using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;
using MouseButtonState = System.Windows.Input.MouseButtonState;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MessageBox = System.Windows.MessageBox;
using System.Security.Principal;
using System.Security.AccessControl;

namespace BodianTaskbarLyric {
    public class TaskbarOverlayWindow : Window {
        private AppConfig _config;
        private string _configPath;
        private BodianEngine _engine;
        private DispatcherTimer _taskbarFollowTimer;
        private Border _rootCard;
        private long _lastLyricClickTick = 0;
        // 圆形黑胶封面与旋转动效
        private Ellipse _coverEllipse;
        private ImageBrush _coverBrush;
        private RotateTransform _coverRotateTransform;
        private DispatcherTimer _rotateTimer;
        private double _currentAngle = 0;

        private StackPanel _lyricPanel;
        private TextBlock _mainText;
        private TextBlock _subText;
        private TranslateTransform _textTranslate;
        private DropShadowEffect _mainShadow;
        private DropShadowEffect _subShadow;

        private string _lastTranslation = "";

        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _lastTaskbarHwnd = IntPtr.Zero;
        private int _zOrderCheckTick = 0;
        private IntPtr _lastFgHwnd = IntPtr.Zero;
        private bool _lastIsFullscreen = false;
        private int _fsCheckTick = 0;
        private Action _onOpenSettings;
        private bool _isSettingsOpen = false;
        private LyricControlPopup _controlPopup;

        private int _lastX = -9999;
        private int _lastY = -9999;
        private int _lastW = -9999;
        private int _lastH = -9999;

        private static readonly Brush HitTestTransparentBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

        private void GetDpiScale(out double dpiX, out double dpiY) {
            dpiX = 1.0;
            dpiY = 1.0;
            try {
                PresentationSource vs = PresentationSource.FromVisual(this);
                if (vs != null && vs.CompositionTarget != null) {
                    dpiX = vs.CompositionTarget.TransformToDevice.M11;
                    dpiY = vs.CompositionTarget.TransformToDevice.M22;
                }
            } catch { }
            if (dpiX <= 0) dpiX = 1.0;
            if (dpiY <= 0) dpiY = 1.0;
        }

        public TaskbarOverlayWindow(AppConfig config, string configPath, BodianEngine engine, Action onOpenSettings) {
            _config = config;
            _configPath = configPath;
            _engine = engine;
            _onOpenSettings = onOpenSettings;

            InitWindow();
            BuildUI();
            ApplyConfig(_config);
            _controlPopup = null;

            // 初始化当前歌曲信息
            if (_engine.CurrentSong != null) {
                _mainText.Text = _engine.CurrentSong.Title;
                _subText.Text = _engine.CurrentSong.Artist;
                _subText.Visibility = Visibility.Visible;
                _mainText.Margin = new Thickness(0, 0, 0, 0);
                ImageSource initCover = _engine.GetCoverImage(_engine.CurrentSong);
                _coverBrush.ImageSource = initCover != null ? initCover : BodianEngine.DefaultCover;
            } else {
                _coverBrush.ImageSource = BodianEngine.DefaultCover;
            }

            _engine.OnSongChanged += Engine_OnSongChanged;
            _engine.OnCoverChanged += Engine_OnCoverChanged;
            _engine.OnLyricChanged += Engine_OnLyricChanged;
            _engine.OnPlayStateChanged += playing => {
                Dispatcher.Invoke(() => {
                    UpdateRotationState();
                    if (_controlPopup != null && _controlPopup.IsOpen) _controlPopup.UpdatePlayState(playing);
                });
            };
            _engine.OnProcessStateChanged += running => {
                Dispatcher.Invoke(() => UpdateVisibility());
            };

            InitCoverRotation();

            _taskbarFollowTimer = new DispatcherTimer();
            _taskbarFollowTimer.Interval = TimeSpan.FromMilliseconds(30);
            _taskbarFollowTimer.Tick += TaskbarFollowTimer_Tick;
            _taskbarFollowTimer.Start();

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) => {
                try {
                    if (_config != null && string.Equals(_config.ColorMode, "system", StringComparison.OrdinalIgnoreCase)) {
                        Dispatcher.Invoke(new Action(() => ApplyConfig(_config)));
                    }
                } catch { }
            };
        }

        private void InitWindow() {
            Title = "BodianTaskbarLyricOverlay";
            Width = _config.Width;
            Height = Math.Max(48, _config.Height);
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = HitTestTransparentBrush;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            SourceInitialized += (s, e) => {
                _hwnd = new WindowInteropHelper(this).Handle;
                HwndSource source = HwndSource.FromHwnd(_hwnd);
                if (source != null) {
                    source.AddHook(WndProc);
                }

                IntPtr hTaskbar = Win32.FindWindow("Shell_TrayWnd", null);
                if (hTaskbar != IntPtr.Zero) {
                    _lastTaskbarHwnd = hTaskbar;
                    Win32.SetWindowLongPtr(_hwnd, Win32.GWL_HWNDPARENT, hTaskbar);
                    Win32.RECT tbRc;
                    if (Win32.GetWindowRect(hTaskbar, out tbRc) && tbRc.Height > 10) {
                        double dpiX, dpiY;
                        GetDpiScale(out dpiX, out dpiY);
                        Height = tbRc.Height / dpiY;
                    }
                }

                int exStyle = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
                Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(exStyle | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST));

                if (hTaskbar != IntPtr.Zero) {
                    EnsureAboveTaskbar(hTaskbar);
                }
            };

            PreviewMouseLeftButtonDown += (s, e) => {
                long now = Environment.TickCount;
                if (now - _lastLyricClickTick < 220) {
                    Program.Log("[Click] Ignored rapid click within 220ms");
                    e.Handled = true;
                    return;
                }
                _lastLyricClickTick = now;

                if (_controlPopup != null && _controlPopup.IsOpen && !_controlPopup.IsClosing) {
                    Program.Log("[Click] Open -> CloseCurrentPopup()");
                    CloseCurrentPopup();
                } else if (_controlPopup == null || !_controlPopup.IsClosing) {
                    Program.Log("[Click] Invoking OpenControlPopup");
                    OpenControlPopup();
                }
                e.Handled = true;
            };

            // 歌词滚轮事件调节系统音量 
            PreviewMouseWheel += (s, e) => {
                if (e.Delta > 0) {
                    int steps = Math.Max(1, e.Delta / 120);
                    for (int i = 0; i < steps; i++) {
                        PlayerController.VolumeUp();
                    }
                } else if (e.Delta < 0) {
                    int steps = Math.Max(1, (-e.Delta) / 120);
                    for (int i = 0; i < steps; i++) {
                        PlayerController.VolumeDown();
                    }
                }
                e.Handled = true;
            };

            // 右键菜单
            System.Windows.Controls.ContextMenu menu = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem miSettings = new System.Windows.Controls.MenuItem { Header = "⚙️ 打开设置中心..." };
            miSettings.Click += (s, e) => { if (_onOpenSettings != null) _onOpenSettings(); };

            Func<string, string> getModeName = (mode) => {
                if (mode == "left") return "最左侧";
                if (mode == "center") return "居中";
                return "天气右侧";
            };

            System.Windows.Controls.MenuItem miMode = new System.Windows.Controls.MenuItem { Header = string.Format("🔄 切换贴靠位置 (当前: {0})", getModeName(_config.PositionMode)) };
            miMode.Click += (s, e) => {
                if (_config.PositionMode == "weather_right") {
                    _config.PositionMode = "left";
                } else if (_config.PositionMode == "left") {
                    _config.PositionMode = "center";
                } else {
                    _config.PositionMode = "weather_right";
                }
                _config.Save(_configPath);
                miMode.Header = string.Format("🔄 切换贴靠位置 (当前: {0})", getModeName(_config.PositionMode));
                _lastX = -9999;
                _lastY = -9999;
            };

            System.Windows.Controls.MenuItem miExit = new System.Windows.Controls.MenuItem { Header = "❌ 退出歌词" };
            miExit.Click += (s, e) => {
                CloseCurrentPopup(immediate: true);
                Application.Current.Shutdown();
            };

            menu.Items.Add(miSettings);
            menu.Items.Add(miMode);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(miExit);
            ContextMenu = menu;
        }

        private void OpenControlPopup() {
            CloseCurrentPopup(immediate: true);
            _controlPopup = new LyricControlPopup(_config, _engine, _onOpenSettings);
            _controlPopup.Closed += (s, e) => {
                if (_controlPopup == s) {
                    _controlPopup = null;
                }
            };
            _controlPopup.ShowNearOverlay(this);
        }

        private void CloseCurrentPopup(bool immediate = false) {
            if (_controlPopup != null) {
                try {
                    _controlPopup.ClosePopup(immediate);
                } catch { }
                if (immediate) {
                    _controlPopup = null;
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {

            if (msg == Win32.WM_MOUSEACTIVATE) {
                handled = true;
                return new IntPtr(Win32.MA_NOACTIVATE); 
            }

            if (msg == Win32.WM_MOUSEWHEEL) {
                short delta = (short)((wParam.ToInt64() >> 16) & 0xffff);
                if (delta > 0) {
                    int steps = Math.Max(1, delta / 120);
                    for (int i = 0; i < steps; i++) {
                        PlayerController.VolumeUp();
                    }
                } else if (delta < 0) {
                    int steps = Math.Max(1, (-delta) / 120);
                    for (int i = 0; i < steps; i++) {
                        PlayerController.VolumeDown();
                    }
                }
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == 0x0084) { // WM_NCHITTEST
                handled = true;
                return new IntPtr(1); // HTCLIENT
            }

            return IntPtr.Zero;
        }

        private void BuildUI() {
            _rootCard = new Border {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 0, 8, 0),
                Background = HitTestTransparentBrush,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = Cursors.Hand
            };

            Grid grid = new Grid { VerticalAlignment = VerticalAlignment.Center, Background = HitTestTransparentBrush };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _coverEllipse = new Ellipse {
                Width = _config.CoverSize,
                Height = _config.CoverSize,
                Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                StrokeThickness = 1.0,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = _config.ShowCover ? new Thickness(0, 0, 8, 0) : new Thickness(0)
            };
            _coverBrush = new ImageBrush {
                Stretch = Stretch.UniformToFill,
                ImageSource = BodianEngine.DefaultCover
            };
            RenderOptions.SetBitmapScalingMode(_coverBrush, BitmapScalingMode.HighQuality);
            _coverEllipse.Fill = _coverBrush;

            // 旋转中心点为圆心
            _coverRotateTransform = new RotateTransform(0, _config.CoverSize / 2.0, _config.CoverSize / 2.0);
            _coverEllipse.RenderTransform = _coverRotateTransform;

            Grid.SetColumn(_coverEllipse, 0);
            grid.Children.Add(_coverEllipse);

            _lyricPanel = new StackPanel {
                VerticalAlignment = VerticalAlignment.Center,
                Background = HitTestTransparentBrush
            };
            _textTranslate = new TranslateTransform(0, 0);
            _lyricPanel.RenderTransform = _textTranslate;

            _mainShadow = new DropShadowEffect {
                BlurRadius = _config.ShadowBlur,
                ShadowDepth = 1,
                Opacity = 0.85,
                Color = Colors.Black
            };
            _subShadow = new DropShadowEffect {
                BlurRadius = _config.ShadowBlur,
                ShadowDepth = 1,
                Opacity = 0.85,
                Color = Colors.Black
            };

            _mainText = new TextBlock {
                Text = "波点音乐 - 任务栏歌词已就绪",
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Effect = _config.ShadowEnabled ? _mainShadow : null
            };

            _subText = new TextBlock {
                Text = "",
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Effect = _config.ShadowEnabled ? _subShadow : null,
                Visibility = Visibility.Collapsed
            };

            _lyricPanel.Children.Add(_mainText);
            _lyricPanel.Children.Add(_subText);

            Grid.SetColumn(_lyricPanel, 1);
            grid.Children.Add(_lyricPanel);

            _rootCard.Child = grid;
            Content = _rootCard;
        }

        public double GetCoverLeftDips() {
            try {
                double dpiX = 1.0;
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null) {
                    dpiX = src.CompositionTarget.TransformToDevice.M11;
                }

                if (_coverEllipse != null && _coverEllipse.IsVisible) {
                    Point screenPt = _coverEllipse.PointToScreen(new Point(0, 0));
                    return screenPt.X / dpiX;
                }

                Win32.RECT rc;
                if (Win32.GetWindowRect(_hwnd, out rc)) {
                    return (rc.Left / dpiX) + 6.0;
                }
            } catch { }
            return Left + 6.0;
        }

        private void InitCoverRotation() {
            _rotateTimer = new DispatcherTimer();
            _rotateTimer.Interval = TimeSpan.FromMilliseconds(30);
            _rotateTimer.Tick += (s, e) => {
                if (_config.ShowCover && _config.RotateCover && _engine.IsPlaying) {
                    _currentAngle = (_currentAngle + 1.2) % 360;
                    _coverRotateTransform.Angle = _currentAngle;
                }
            };
            _rotateTimer.Start();
        }

        public void UpdateRotationState() {
            if (!_config.RotateCover) {
                _coverRotateTransform.Angle = 0;
                _currentAngle = 0;
            }
        }

        public void ApplyConfig(AppConfig cfg) {
            _config = cfg;
            Width = _config.Width;

            double dpiX, dpiY;
            GetDpiScale(out dpiX, out dpiY);

            IntPtr hTaskbar = Win32.FindWindow("Shell_TrayWnd", null);
            Win32.RECT rc;
            if (hTaskbar != IntPtr.Zero && Win32.GetWindowRect(hTaskbar, out rc) && rc.Height > 10) {
                Height = rc.Height / dpiY;
            } else {
                Height = _config.Height;
            }

            FontFamily fam = new FontFamily(_config.FontFamily);
            _mainText.FontFamily = fam;
            _mainText.FontSize = _config.MainFontSize;

            _subText.FontFamily = fam;
            _subText.FontSize = _config.SubFontSize;

            if (!_config.ShowTranslation) {
                _subText.Visibility = Visibility.Collapsed;
            } else if (!string.IsNullOrEmpty(_lastTranslation)) {
                _subText.Text = _lastTranslation;
                _subText.Visibility = Visibility.Visible;
            }

            if (_config.ShowCover) {
                _coverEllipse.Visibility = Visibility.Visible;
                _coverEllipse.Margin = new Thickness(0, 0, 8, 0);
                ImageSource curCover = (_engine.CurrentSong != null) 
                    ? _engine.GetCoverImage(_engine.CurrentSong) 
                    : BodianEngine.DefaultCover;
                _coverBrush.ImageSource = curCover != null ? curCover : BodianEngine.DefaultCover;
            } else {
                _coverEllipse.Visibility = Visibility.Collapsed;
                _coverEllipse.Margin = new Thickness(0, 0, 0, 0);
            }

            _coverEllipse.Width = _config.CoverSize;
            _coverEllipse.Height = _config.CoverSize;
            _coverRotateTransform.CenterX = _config.CoverSize / 2.0;
            _coverRotateTransform.CenterY = _config.CoverSize / 2.0;

            bool isDark = Win32.IsSystemDarkTheme();
            Brush mainBrush, subBrush;

            string colorMode = (_config.ColorMode ?? (_config.AutoTheme ? "system" : "custom")).ToLowerInvariant();

            if (colorMode == "white") {
                mainBrush = Brushes.White;
                subBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
                _mainShadow.Color = Colors.Black;
                _subShadow.Color = Colors.Black;
            } else if (colorMode == "black") {
                mainBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                subBrush = new SolidColorBrush(Color.FromArgb(200, 70, 70, 70));
                _mainShadow.Color = Colors.White;
                _subShadow.Color = Colors.White;
            } else if (colorMode == "custom") {
                Color customC;
                if (!ColorHelper.TryParseColor(_config.CustomColor, out customC)) {
                    if (!ColorHelper.TryParseColor(_config.MainTextColor, out customC)) {
                        customC = Colors.White;
                    }
                }
                mainBrush = new SolidColorBrush(customC);
                subBrush = new SolidColorBrush(Color.FromArgb((byte)(customC.A * 0.78), customC.R, customC.G, customC.B));
                double luminance = (0.299 * customC.R + 0.587 * customC.G + 0.114 * customC.B) / 255.0;
                Color shadowCol = (luminance > 0.45) ? Colors.Black : Colors.White;
                _mainShadow.Color = shadowCol;
                _subShadow.Color = shadowCol;
            } else { // "system"
                mainBrush = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(30, 30, 30));
                subBrush = isDark ? new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(200, 70, 70, 70));
                _mainShadow.Color = isDark ? Colors.Black : Colors.White;
                _subShadow.Color = isDark ? Colors.Black : Colors.White;
            }

            _mainText.Foreground = mainBrush;
            _subText.Foreground = subBrush;

            _mainShadow.BlurRadius = _config.ShadowBlur;
            _subShadow.BlurRadius = _config.ShadowBlur;
            _mainText.Effect = _config.ShadowEnabled ? _mainShadow : null;
            _subText.Effect = _config.ShadowEnabled ? _subShadow : null;

            if (_config.ShowBackgroundCard) {
                _rootCard.Background = (Brush)new BrushConverter().ConvertFromString(_config.BgCardColor);
            } else {
                _rootCard.Background = HitTestTransparentBrush;
            }

            UpdateRotationState();
            UpdateVisibility();
            if (_controlPopup != null && _controlPopup.IsOpen) {
                _controlPopup.ApplyTheme();
            }
            _lastX = -9999;
            _lastY = -9999;
        }

        public void UpdateXOffset(int xOffset) {
            _config.XOffset = xOffset;
            _lastX = -9999;
        }

        public void UpdateYOffset(int yOffset) {
            _config.YOffset = yOffset;
            _lastY = -9999;
        }

        public void UpdateWidth(int width) {
            _config.Width = width;
            Width = width;
            _lastW = -9999;
            _lastX = -9999;
        }

        public void UpdateFontSizes(double mainSize, double subSize) {
            _config.MainFontSize = mainSize;
            _config.SubFontSize = subSize;
            if (_mainText != null) _mainText.FontSize = mainSize;
            if (_subText != null) _subText.FontSize = subSize;
        }

        public void SetSettingsWindowOpen(bool isOpen) {
            _isSettingsOpen = isOpen;
            if (isOpen) {
                if (!_engine.IsBodianRunning && _engine.CurrentSong == null) {
                    _mainText.Text = "波点音乐 - 任务栏歌词预览";
                    _subText.Text = "设置预览模式";
                    _subText.Visibility = _config.ShowTranslation ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            UpdateVisibility();
        }

        // 全屏检测
        public bool IsFullscreenActive(IntPtr hTaskbar) {
            try {
                Win32.QUERY_USER_NOTIFICATION_STATE state;
                if (Win32.SHQueryUserNotificationState(out state) == 0) {
                    if (state == Win32.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                        state == Win32.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE) {
                        return true;
                    }
                }

                if (hTaskbar == IntPtr.Zero) return false;

                _fsCheckTick++;
                IntPtr fg = Win32.GetForegroundWindow();
                if (fg == _lastFgHwnd && _fsCheckTick < 10) {
                    return _lastIsFullscreen;
                }

                _fsCheckTick = 0;
                _lastFgHwnd = fg;

                IntPtr hMon = Win32.MonitorFromWindow(hTaskbar, Win32.MONITOR_DEFAULTTOPRIMARY);
                if (hMon == IntPtr.Zero) {
                    _lastIsFullscreen = false;
                    return false;
                }

                Win32.MONITORINFO mi = new Win32.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(mi);
                if (!Win32.GetMonitorInfo(hMon, ref mi)) {
                    _lastIsFullscreen = false;
                    return false;
                }

                if (fg != IntPtr.Zero && fg != hTaskbar && fg != _hwnd) {
                    IntPtr fgMon = Win32.MonitorFromWindow(fg, Win32.MONITOR_DEFAULTTONULL);
                    if (fgMon == hMon && CheckWindowCoversMonitor(fg, mi.rcMonitor)) {
                        _lastIsFullscreen = true;
                        return true;
                    }
                }

                Win32.RECT tbRc;
                if (Win32.GetWindowRect(hTaskbar, out tbRc)) {
                    Win32.POINT pt = new Win32.POINT();
                    pt.X = tbRc.Left + (tbRc.Right - tbRc.Left) / 2;
                    pt.Y = tbRc.Top + (tbRc.Bottom - tbRc.Top) / 2;
                    IntPtr hUnder = Win32.WindowFromPoint(pt);
                    if (hUnder != IntPtr.Zero) {
                        IntPtr root = Win32.GetAncestor(hUnder, Win32.GA_ROOT);
                        if (root != IntPtr.Zero && root != hTaskbar && root != _hwnd) {
                            if (CheckWindowCoversMonitor(root, mi.rcMonitor)) {
                                _lastIsFullscreen = true;
                                return true;
                            }
                        }
                    }
                }

                _lastIsFullscreen = false;
                return false;
            } catch {
                return false;
            }
        }

        private static bool CheckWindowCoversMonitor(IntPtr hWnd, Win32.RECT monRc) {
            if (hWnd == IntPtr.Zero || !Win32.IsWindowVisible(hWnd) || Win32.IsIconic(hWnd)) return false;

            StringBuilder sbCls = new StringBuilder(256);
            Win32.GetClassName(hWnd, sbCls, 256);
            string cls = sbCls.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") {
                return false;
            }

            if (cls == "Windows.UI.Core.CoreWindow" || cls == "XamlExplorerHostIslandWindow" || cls == "Shell_Dialog") {
                return false;
            }

            Win32.RECT rc;
            if (Win32.DwmGetWindowAttribute(hWnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out rc, Marshal.SizeOf(typeof(Win32.RECT))) != 0) {
                Win32.GetWindowRect(hWnd, out rc);
            }

            return (rc.Left <= monRc.Left + 2 &&
                    rc.Top <= monRc.Top + 2 &&
                    rc.Right >= monRc.Right - 2 &&
                    rc.Bottom >= monRc.Bottom - 2);
        }

        private static bool IsTaskbarWindow(IntPtr hWnd, IntPtr hTaskbar) {
            if (hWnd == IntPtr.Zero) return false;
            if (hWnd == hTaskbar) return true;
            IntPtr root = Win32.GetAncestor(hWnd, Win32.GA_ROOT);
            if (root == hTaskbar) return true;
            StringBuilder sb = new StringBuilder(64);
            Win32.GetClassName(hWnd, sb, 64);
            string cls = sb.ToString();
            return cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls == "TrayWindow" || cls == "XamlExplorerHostIslandWindow";
        }

        public void EnsureAboveTaskbar(IntPtr hTaskbar) {
            try {
                if (_hwnd == IntPtr.Zero || hTaskbar == IntPtr.Zero) return;

                bool isBehindTaskbar = false;

                IntPtr cur = Win32.GetWindow(_hwnd, Win32.GW_HWNDPREV);
                int depth = 0;
                while (cur != IntPtr.Zero && depth < 100) {
                    if (cur == hTaskbar || Win32.GetAncestor(cur, Win32.GA_ROOT) == hTaskbar) {
                        isBehindTaskbar = true;
                        break;
                    }
                    StringBuilder sbCls = new StringBuilder(64);
                    Win32.GetClassName(cur, sbCls, 64);
                    string cls = sbCls.ToString();
                    if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd" || cls == "TrayWindow") {
                        isBehindTaskbar = true;
                        break;
                    }
                    cur = Win32.GetWindow(cur, Win32.GW_HWNDPREV);
                    depth++;
                }

                if (!isBehindTaskbar) {
                    cur = Win32.GetWindow(hTaskbar, Win32.GW_HWNDNEXT);
                    depth = 0;
                    while (cur != IntPtr.Zero && depth < 100) {
                        if (cur == _hwnd) {
                            isBehindTaskbar = true;
                            break;
                        }
                        cur = Win32.GetWindow(cur, Win32.GW_HWNDNEXT);
                        depth++;
                    }
                }

                if (isBehindTaskbar) {
                    Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                    Win32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            } catch { }
        }

        public bool UpdateVisibility(IntPtr hTaskbar, Win32.RECT rc, double dpiScaleY) {
            try {
                int screenW = Win32.GetSystemMetrics(0);
                int screenH = Win32.GetSystemMetrics(1);
                int hideThreshold = Math.Max(4, (int)(8 * dpiScaleY));

                int monLeft = 0, monTop = 0, monRight = screenW, monBottom = screenH;
                IntPtr hMon = Win32.MonitorFromWindow(hTaskbar, Win32.MONITOR_DEFAULTTOPRIMARY);
                if (hMon != IntPtr.Zero) {
                    Win32.MONITORINFO mi = new Win32.MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(Win32.MONITORINFO));
                    if (Win32.GetMonitorInfo(hMon, ref mi)) {
                        monLeft = mi.rcMonitor.Left;
                        monTop = mi.rcMonitor.Top;
                        monRight = mi.rcMonitor.Right;
                        monBottom = mi.rcMonitor.Bottom;
                    }
                }

                bool isHorizontal = rc.Width >= rc.Height;
                int visibleThickness = isHorizontal
                    ? (Math.Min(rc.Bottom, monBottom) - Math.Max(rc.Top, monTop))
                    : (Math.Min(rc.Right, monRight) - Math.Max(rc.Left, monLeft));

                bool taskbarHidden = _config.AutoHideWithTaskbar && (visibleThickness <= hideThreshold);
                if (taskbarHidden) {
                    if (Visibility != Visibility.Hidden) Visibility = Visibility.Hidden;
                    return false;
                }

                if (_config.HideWhenFullscreen && IsFullscreenActive(hTaskbar)) {
                    if (Visibility != Visibility.Hidden) Visibility = Visibility.Hidden;
                    return false;
                }

                if (!_engine.IsBodianRunning && !_isSettingsOpen) {
                    if (Visibility != Visibility.Hidden) Visibility = Visibility.Hidden;
                    return false;
                }

                if (Visibility != Visibility.Visible) {
                    Visibility = Visibility.Visible;
                    _lastX = -9999;
                    _lastY = -9999;
                    EnsureAboveTaskbar(hTaskbar);
                }
                return true;
            } catch {
                return false;
            }
        }

        public void UpdateVisibility() {
            try {
                IntPtr hTaskbar = Win32.FindWindow("Shell_TrayWnd", null);
                if (hTaskbar == IntPtr.Zero || _hwnd == IntPtr.Zero) return;

                Win32.RECT rc;
                if (!Win32.GetWindowRect(hTaskbar, out rc)) return;

                double dpiX, dpiY;
                GetDpiScale(out dpiX, out dpiY);
                UpdateVisibility(hTaskbar, rc, dpiY);
            } catch { }
        }

        private void TaskbarFollowTimer_Tick(object sender, EventArgs e) {
            try {
                IntPtr hTaskbar = Win32.FindWindow("Shell_TrayWnd", null);
                if (hTaskbar == IntPtr.Zero || _hwnd == IntPtr.Zero) return;

                if (hTaskbar != _lastTaskbarHwnd) {
                    _lastTaskbarHwnd = hTaskbar;
                    Win32.SetWindowLongPtr(_hwnd, Win32.GWL_HWNDPARENT, hTaskbar);

                    int exStyle = Win32.GetWindowLong(_hwnd, Win32.GWL_EXSTYLE);
                    Win32.SetWindowLongPtr(_hwnd, Win32.GWL_EXSTYLE, new IntPtr(exStyle | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST));

                    EnsureAboveTaskbar(hTaskbar);
                }

                Win32.RECT rc;
                if (!Win32.GetWindowRect(hTaskbar, out rc)) return;

                double dpiScaleX, dpiScaleY;
                GetDpiScale(out dpiScaleX, out dpiScaleY);

                if (!UpdateVisibility(hTaskbar, rc, dpiScaleY)) {
                    return;
                }

                IntPtr fg = Win32.GetForegroundWindow();
                if (fg == hTaskbar || (fg != _lastFgHwnd && IsTaskbarWindow(fg, hTaskbar))) {
                    EnsureAboveTaskbar(hTaskbar);
                }

                _zOrderCheckTick++;
                if (_zOrderCheckTick >= 3) {
                    _zOrderCheckTick = 0;
                    EnsureAboveTaskbar(hTaskbar);
                }

                int targetY = rc.Top + (int)(_config.YOffset * dpiScaleY);
                int targetW = (int)(_config.Width * dpiScaleX);
                int targetH = rc.Height;

                int targetX = rc.Left + (int)(_config.XOffset * dpiScaleX);
                if (_config.PositionMode == "weather_right") {
                    targetX += (int)(200 * dpiScaleX);
                } else if (_config.PositionMode == "center") {
                    int baseCenterX = rc.Width > targetW ? rc.Left + (rc.Width - targetW) / 2 : rc.Left;
                    targetX = baseCenterX + (int)(_config.XOffset * dpiScaleX);
                }

                if (targetX == _lastX && targetY == _lastY && targetW == _lastW && targetH == _lastH) {
                    return;
                }

                _lastX = targetX;
                _lastY = targetY;
                _lastW = targetW;
                _lastH = targetH;

                Win32.SetWindowPos(_hwnd, IntPtr.Zero, targetX, targetY, targetW, targetH, Win32.SWP_NOACTIVATE | Win32.SWP_NOZORDER);
                EnsureAboveTaskbar(hTaskbar);
            } catch (Exception ex) {
                Program.Log("[TaskbarFollowTimer] EX: " + ex.ToString());
            }
        }

        private void Engine_OnSongChanged(SongInfo song, ImageSource cover) {
            Dispatcher.Invoke(() => {
                _lastTranslation = "";
                _coverBrush.ImageSource = cover != null ? cover : BodianEngine.DefaultCover;
                _mainText.Text = song.Title;
                _subText.Text = song.Artist;
                _subText.Visibility = Visibility.Visible;
                _mainText.Margin = new Thickness(0, 0, 0, 0);
                UpdateRotationState();
                if (_controlPopup != null && _controlPopup.IsOpen) {
                    _controlPopup.UpdateSong(song, cover);
                }
            });
        }

        private void Engine_OnCoverChanged(ImageSource cover) {
            Dispatcher.Invoke(() => {
                _coverBrush.ImageSource = cover != null ? cover : BodianEngine.DefaultCover;
                if (_controlPopup != null && _controlPopup.IsOpen) {
                    _controlPopup.UpdateCover(cover);
                }
            });
        }

        private void Engine_OnLyricChanged(LyricLine line, int index) {
            Dispatcher.Invoke(() => {
                try {
                    string orig = line.Original;
                    string trans = line.Translation;
                    _lastTranslation = trans;

                    if (string.IsNullOrEmpty(orig) && _engine.CurrentSong != null) {
                        orig = _engine.CurrentSong.Title;
                    }

                    _mainText.Text = orig;

                    if (_config.ShowTranslation && !string.IsNullOrEmpty(trans)) {
                        _subText.Text = trans;
                        _subText.Visibility = Visibility.Visible;
                    } else {
                        _subText.Visibility = Visibility.Collapsed;
                    }
                    _mainText.Margin = new Thickness(0, 0, 0, 0);

                    TriggerTransitionAnimation();
                } catch (Exception ex) {
                    Program.Log("[Overlay] Engine_OnLyricChanged EXCEPTION: " + ex);
                }
            });
        }

        private void TriggerTransitionAnimation() {
            if (_config.AnimationType == "None") {
                _textTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                _textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                _lyricPanel.BeginAnimation(UIElement.OpacityProperty, null);
                _textTranslate.X = 0;
                _textTranslate.Y = 0;
                _lyricPanel.Opacity = 1.0;
                return;
            }

            double durationSec = Math.Max(0.05, _config.AnimationDurationMs / 1000.0);
            Duration dur = new Duration(TimeSpan.FromSeconds(durationSec));

            if (_config.AnimationType == "SlideFade") {
                DoubleAnimation slideAnim = new DoubleAnimation(7.0, 0.0, dur) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                DoubleAnimation fadeAnim = new DoubleAnimation(0.2, 1.0, dur) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                _textTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                _textTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
                _lyricPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            } else if (_config.AnimationType == "HorizontalSweep") {
                DoubleAnimation sweepAnim = new DoubleAnimation(-18.0, 0.0, dur) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                DoubleAnimation fadeAnim = new DoubleAnimation(0.2, 1.0, dur) {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                _textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                _textTranslate.BeginAnimation(TranslateTransform.XProperty, sweepAnim);
                _lyricPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            } else if (_config.AnimationType == "FadeOnly") {
                _textTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                _textTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                DoubleAnimation fadeAnim = new DoubleAnimation(0.0, 1.0, dur);
                _lyricPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
        }
    }

}
