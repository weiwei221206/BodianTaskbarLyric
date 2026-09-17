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
    public class LyricControlPopup : Window {
        private const double CARD_WIDTH = 280;
        private const double CARD_HEIGHT = 120;
        private const double PADDING_H = 14;
        private const double PADDING_TOP = 14;
        private const double PADDING_BOTTOM = 8;

        private AppConfig _config;
        private BodianEngine _engine;
        private Action _onOpenSettings;

        private Border _cardBorder;
        private Grid _cardShadowHost;
        private ScaleTransform _cardScale;
        private Ellipse _coverEllipse;
        private ImageBrush _coverBrush;
        private TextBlock _titleText;
        private TextBlock _artistText;
        private long _showTick = 0;
        private bool _isOpen = false;
        private bool _isClosing = false;
        public bool IsOpen { get { return _isOpen; } }
        public bool IsClosing { get { return _isClosing; } }
        private Microsoft.Win32.UserPreferenceChangedEventHandler _userPrefHandler;

        // 控制按钮
        private Border _btnSettings;
        private Border _btnOpenApp;
        private Border _btnPrev;
        private Border _btnPlayPause;
        private Border _btnNext;

        private System.Windows.Shapes.Path _iconPrev;
        private System.Windows.Shapes.Path _iconPlayPause;
        private System.Windows.Shapes.Path _iconNext;

        // 播放进度
        private Grid _progressContainer;
        private Border _progressTrack;
        private Border _progressFill;
        private Border _progressThumb;
        private DispatcherTimer _progressTimer;
        private bool _isDraggingProgress = false;

        private bool _isDarkTheme = true;
        private Brush _primaryTextBrush;
        private Brush _secondaryTextBrush;
        private Brush _iconBrush;
        private Brush _btnHoverBrush;
        private Brush _progressTrackBrush;
        private Brush _progressFillBrush = new SolidColorBrush(Color.FromRgb(0, 210, 106));

        private Geometry _geomPlay;
        private Geometry _geomPause;
        private Geometry _geomPrev;
        private Geometry _geomNext;

        public long LastHideTick { get; private set; }

        public LyricControlPopup(AppConfig config, BodianEngine engine, Action onOpenSettings) {
            _config = config;
            _engine = engine;
            _onOpenSettings = onOpenSettings;
            LastHideTick = 0;

            InitWindow();
            InitGeometries();
            BuildUI();
            ApplyTheme();

            Deactivated += (s, e) => {
                if (Environment.TickCount - _showTick < 280) {
                    Program.Log("[Popup] Ignored spurious Deactivated during opening (tick diff: " + (Environment.TickCount - _showTick) + ")");
                    return;
                }
                Program.Log("[Popup] Deactivated -> close");
                ClosePopup();
            };

            _userPrefHandler = (s, e) => {
                try {
                    Dispatcher.Invoke(new Action(() => ApplyTheme()));
                } catch { }
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += _userPrefHandler;

            _progressTimer = new DispatcherTimer();
            _progressTimer.Interval = TimeSpan.FromMilliseconds(150);
            _progressTimer.Tick += (s, e) => {
                if (!_isDraggingProgress && IsVisible) {
                    UpdateProgressUI();
                }
            };
        }

        // 关闭弹窗并支持平滑收起动效
        public void ClosePopup(bool immediate = false) {
            if (!_isOpen || _isClosing) {
                if (immediate && _isClosing) {
                    try { Close(); } catch { }
                }
                return;
            }
            _isClosing = true;
            _isOpen = false;
            LastHideTick = Environment.TickCount;

            if (_progressTimer != null) {
                _progressTimer.Stop();
                _progressTimer = null;
            }

            if (_userPrefHandler != null) {
                try {
                    Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _userPrefHandler;
                } catch { }
                _userPrefHandler = null;
            }

            if (_cardBorder != null) {
                _cardBorder.IsHitTestVisible = false;
            }

            if (immediate) {
                try {
                    Close();
                } catch { }
                return;
            }

            PlayExitAnimation(() => {
                try {
                    Close();
                } catch { }
            });
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            _isOpen = false;
            if (_progressTimer != null) {
                _progressTimer.Stop();
                _progressTimer = null;
            }
            if (_userPrefHandler != null) {
                try {
                    Microsoft.Win32.SystemEvents.UserPreferenceChanged -= _userPrefHandler;
                } catch { }
                _userPrefHandler = null;
            }
        }

        private void InitWindow() {
            Title = "BodianMiniControl";
            Width = CARD_WIDTH + PADDING_H * 2;
            Height = CARD_HEIGHT + PADDING_TOP + PADDING_BOTTOM;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            FontFamily = new FontFamily("Segoe UI Variable Text, PingFang SC, Microsoft YaHei UI");
            try {
                IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
                int exStyle = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE);
                Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, new IntPtr(exStyle | Win32.WS_EX_TOOLWINDOW));
            } catch { }
        }

        private void InitGeometries() {
            _geomPrev = Geometry.Parse("M 1.5,1.5 L 1.5,12.5 M 1.5,7 L 11,1.5 L 11,12.5 Z");
            _geomNext = Geometry.Parse("M 1,1.5 L 10.5,7 L 1,12.5 Z M 10.5,1.5 L 10.5,12.5");
            _geomPlay = Geometry.Parse("M 3.5,1.5 L 13,7.5 L 3.5,13.5 Z");
            _geomPause = Geometry.Parse("M 3,1.5 L 5.5,1.5 L 5.5,13.5 L 3,13.5 Z M 8.5,1.5 L 11,1.5 L 11,13.5 L 8.5,13.5 Z");
        }

        private void BuildUI() {
            Grid rootGrid = new Grid();

            _cardScale = new ScaleTransform(0.94, 0.94);

            // 阴影
            _cardShadowHost = new Grid {
                Effect = new DropShadowEffect {
                    BlurRadius = 14,
                    ShadowDepth = 2,
                    Direction = 270,
                    Opacity = 0.20,
                    Color = Colors.Black
                }
            };

            _cardBorder = new Border {
                Width = CARD_WIDTH,
                Height = CARD_HEIGHT,
                Margin = new Thickness(PADDING_H, PADDING_TOP, PADDING_H, PADDING_BOTTOM),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 8, 14, 11),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 1.0),
                RenderTransform = _cardScale,
                Opacity = 0.0
            };

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 第一行：歌曲信息
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 第二行：控制按钮
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 第三行：进度条

            // 第一行
            Grid topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _coverEllipse = new Ellipse {
                Width = 40,
                Height = 40,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                StrokeThickness = 1.0
            };
            _coverBrush = new ImageBrush {
                Stretch = Stretch.UniformToFill,
                ImageSource = BodianEngine.DefaultCover
            };
            RenderOptions.SetBitmapScalingMode(_coverBrush, BitmapScalingMode.HighQuality);
            _coverEllipse.Fill = _coverBrush;
            Grid.SetColumn(_coverEllipse, 0);
            topRow.Children.Add(_coverEllipse);

            StackPanel textPanel = new StackPanel {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _titleText = new TextBlock {
                Text = "波点音乐",
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _artistText = new TextBlock {
                Text = "当前未在播放",
                FontSize = 11.5,
                Margin = new Thickness(0, 1.5, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(_titleText);
            textPanel.Children.Add(_artistText);
            Grid.SetColumn(textPanel, 1);
            topRow.Children.Add(textPanel);

            // 右上角
            Image appIconImg = new Image {
                Width = 18,
                Height = 18,
                Stretch = Stretch.Uniform,
                Source = BodianEngine.GetAppIconImageSource(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(appIconImg, BitmapScalingMode.HighQuality);

            _btnSettings = CreateMediaButton(appIconImg, 26, () => {
                ClosePopup();
                if (_onOpenSettings != null) _onOpenSettings();
            }, "打开歌词设置中心");
            _btnSettings.VerticalAlignment = VerticalAlignment.Top;
            _btnSettings.Margin = new Thickness(0, -2, -2, 0);
            Grid.SetColumn(_btnSettings, 2);
            topRow.Children.Add(_btnSettings);

            Grid.SetRow(topRow, 0);
            mainGrid.Children.Add(topRow);

            // 第二行
            Grid btnGrid = new Grid {
                Margin = new Thickness(0, 5, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 左
            Image bodianClientImg = new Image {
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform,
                Source = BodianEngine.GetBodianClientIconImageSource(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(bodianClientImg, BitmapScalingMode.HighQuality);

            _btnOpenApp = CreateMediaButton(bodianClientImg, 30, () => {
                PlayerController.OpenBodianApp();
            }, "打开波点音乐客户端");
            Grid.SetColumn(_btnOpenApp, 0);
            btnGrid.Children.Add(_btnOpenApp);

            // 右
            StackPanel rightPlaybackPanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _iconPrev = new System.Windows.Shapes.Path {
                Data = _geomPrev,
                Stretch = Stretch.Uniform,
                Width = 12,
                Height = 12,
                StrokeThickness = 1.2
            };
            _btnPrev = CreateMediaButton(_iconPrev, 32, () => {
                PlayerController.PreviousTrack();
            }, "上一首");

            _iconPlayPause = new System.Windows.Shapes.Path {
                Data = _engine != null && _engine.IsPlaying ? _geomPause : _geomPlay,
                Stretch = Stretch.Uniform,
                Width = 13,
                Height = 13,
                StrokeThickness = 1.2
            };
            _btnPlayPause = CreateMediaButton(_iconPlayPause, 36, () => {
                PlayerController.PlayOrPause();
                bool willPlay = _engine != null ? !_engine.IsPlaying : true;
                _iconPlayPause.Data = willPlay ? _geomPause : _geomPlay;
            }, "播放 / 暂停");

            _iconNext = new System.Windows.Shapes.Path {
                Data = _geomNext,
                Stretch = Stretch.Uniform,
                Width = 12,
                Height = 12,
                StrokeThickness = 1.2
            };
            _btnNext = CreateMediaButton(_iconNext, 32, () => {
                PlayerController.NextTrack();
            }, "下一首");

            rightPlaybackPanel.Children.Add(_btnPrev);
            rightPlaybackPanel.Children.Add(_btnPlayPause);
            rightPlaybackPanel.Children.Add(_btnNext);
            Grid.SetColumn(rightPlaybackPanel, 1);
            btnGrid.Children.Add(rightPlaybackPanel);

            Grid.SetRow(btnGrid, 1);
            mainGrid.Children.Add(btnGrid);

            // 第三行
            _progressContainer = new Grid {
                Height = 14,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _progressContainer.SizeChanged += (s, e) => {
                if (e.NewSize.Width > 0) {
                    UpdateProgressUI();
                }
            };

            _progressTrack = new Border {
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _progressContainer.Children.Add(_progressTrack);

            _progressFill = new Border {
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Background = _progressFillBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            _progressContainer.Children.Add(_progressFill);

            _progressThumb = new Border {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 0),
                Effect = new DropShadowEffect {
                    BlurRadius = 2.5,
                    ShadowDepth = 0.8,
                    Opacity = 0.25,
                    Color = Colors.Black
                }
            };
            _progressContainer.Children.Add(_progressThumb);

            Action<System.Windows.Input.MouseEventArgs> handleProgressInput = (e) => {
                double w = _progressContainer.ActualWidth;
                if (w <= 8) return;
                Point p = e.GetPosition(_progressContainer);
                double pct = Math.Max(0.0, Math.Min(1.0, p.X / w));
                double total = (_engine != null && _engine.CurrentSong != null) ? _engine.CurrentSong.Duration : 0;
                double cur = total * pct;

                double thumbLeft = (w - 8.0) * pct;
                _progressThumb.Margin = new Thickness(thumbLeft, 0, 0, 0);
                _progressFill.Width = (pct <= 0) ? 0 : ((pct >= 1.0) ? w : (thumbLeft + 4.0));
            };

            _progressContainer.MouseLeftButtonDown += (s, e) => {
                _isDraggingProgress = true;
                _progressContainer.CaptureMouse();
                handleProgressInput(e);
                e.Handled = true;
            };

            _progressContainer.MouseMove += (s, e) => {
                if (_isDraggingProgress) {
                    handleProgressInput(e);
                    e.Handled = true;
                }
            };

            _progressContainer.MouseLeftButtonUp += (s, e) => {
                if (_isDraggingProgress) {
                    _isDraggingProgress = false;
                    _progressContainer.ReleaseMouseCapture();
                    handleProgressInput(e);

                    double w = _progressContainer.ActualWidth;
                    if (w > 0 && _engine != null && _engine.CurrentSong != null && _engine.CurrentSong.Duration > 0) {
                        Point p = e.GetPosition(_progressContainer);
                        double pct = Math.Max(0.0, Math.Min(1.0, p.X / w));
                        double targetSeconds = _engine.CurrentSong.Duration * pct;
                        _engine.Seek(targetSeconds);
                    }

                    e.Handled = true;
                }
            };

            _progressContainer.LostMouseCapture += (s, e) => {
                _isDraggingProgress = false;
            };

            Grid.SetRow(_progressContainer, 2);
            mainGrid.Children.Add(_progressContainer);

            _cardBorder.Child = mainGrid;
            _cardShadowHost.Children.Add(_cardBorder);
            rootGrid.Children.Add(_cardShadowHost);
            Content = rootGrid;
        }

        private Border CreateMediaButton(UIElement icon, double size, Action onClick, string tooltipText = null) {
            Border btn = new Border {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2.0),
                Background = Brushes.Transparent,
                Margin = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand,
                Child = icon,
                ToolTip = tooltipText
            };

            btn.MouseEnter += (s, e) => {
                btn.Background = _btnHoverBrush;
            };
            btn.MouseLeave += (s, e) => {
                btn.Background = Brushes.Transparent;
            };
            btn.MouseLeftButtonDown += (s, e) => {
                btn.RenderTransform = new ScaleTransform(0.92, 0.92, size / 2.0, size / 2.0);
            };
            btn.MouseLeftButtonUp += (s, e) => {
                btn.RenderTransform = null;
                if (onClick != null) onClick();
                e.Handled = true;
            };

            return btn;
        }

        public void ApplyTheme() {
            bool isDark = false;
            if (_config != null && string.Equals(_config.UITheme, "light", StringComparison.OrdinalIgnoreCase)) {
                isDark = false;
            } else if (_config != null && string.Equals(_config.UITheme, "dark", StringComparison.OrdinalIgnoreCase)) {
                isDark = true;
            } else {
                isDark = Win32.IsSystemDarkTheme();
            }

            _isDarkTheme = isDark;

            if (_isDarkTheme) {
                // 深色主题
                _cardBorder.Background = new SolidColorBrush(Color.FromArgb(175, 24, 24, 26));
                _cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
                _primaryTextBrush = Brushes.White;
                _secondaryTextBrush = new SolidColorBrush(Color.FromArgb(175, 255, 255, 255));
                _iconBrush = Brushes.White;
                _btnHoverBrush = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                _coverEllipse.Stroke = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));
                _progressTrackBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            } else {
                // 浅色主题
                _cardBorder.Background = new SolidColorBrush(Color.FromArgb(195, 255, 255, 255));
                _cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0));
                _primaryTextBrush = new SolidColorBrush(Color.FromRgb(28, 28, 30));
                _secondaryTextBrush = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                _iconBrush = new SolidColorBrush(Color.FromRgb(28, 28, 30));
                _btnHoverBrush = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0));
                _coverEllipse.Stroke = new SolidColorBrush(Color.FromArgb(28, 0, 0, 0));
                _progressTrackBrush = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
            }

            _titleText.Foreground = _primaryTextBrush;
            _artistText.Foreground = _secondaryTextBrush;
            if (_progressTrack != null) _progressTrack.Background = _progressTrackBrush;

            if (_iconPrev != null) { _iconPrev.Fill = _iconBrush; _iconPrev.Stroke = _iconBrush; }
            if (_iconPlayPause != null) { _iconPlayPause.Fill = _iconBrush; _iconPlayPause.Stroke = _iconBrush; }
            if (_iconNext != null) { _iconNext.Fill = _iconBrush; _iconNext.Stroke = _iconBrush; }
        }

        private static string FormatTime(double seconds) {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
            int totalSec = (int)Math.Round(seconds);
            int m = totalSec / 60;
            int s = totalSec % 60;
            return string.Format("{0:D2}:{1:D2}", m, s);
        }

        private void UpdateProgressUI() {
            if (_engine == null) return;
            double curSec = _engine.CurrentPlaybackSeconds;
            double totalSec = (_engine.CurrentSong != null) ? _engine.CurrentSong.Duration : 0;

            if (_progressContainer != null && totalSec > 0) {
                _progressContainer.ToolTip = string.Format("{0} / {1}", FormatTime(curSec), FormatTime(totalSec));
            } else if (_progressContainer != null) {
                _progressContainer.ToolTip = null;
            }

            double w = (_progressContainer != null && _progressContainer.ActualWidth > 0) 
                ? _progressContainer.ActualWidth 
                : (CARD_WIDTH - PADDING_H * 2 - 2); 

            if (w > 8 && totalSec > 0) {
                double pct = Math.Max(0.0, Math.Min(1.0, curSec / totalSec));
                double thumbLeft = (w - 8.0) * pct;
                _progressThumb.Margin = new Thickness(thumbLeft, 0, 0, 0);
                _progressFill.Width = (pct <= 0) ? 0 : ((pct >= 1.0) ? w : (thumbLeft + 4.0));
            } else {
                _progressFill.Width = 0;
                _progressThumb.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        public void UpdateSong(SongInfo song, ImageSource cover) {
            Dispatcher.Invoke(() => {
                if (song != null) {
                    _titleText.Text = !string.IsNullOrEmpty(song.Title) ? song.Title : "未知歌曲";
                    _artistText.Text = !string.IsNullOrEmpty(song.Artist) ? song.Artist : "未知歌手";
                    _titleText.ToolTip = _titleText.Text;
                    _artistText.ToolTip = _artistText.Text;
                } else {
                    _titleText.Text = "波点音乐";
                    _artistText.Text = "当前未在播放";
                    _titleText.ToolTip = null;
                    _artistText.ToolTip = null;
                }
                if (cover != null) {
                    _coverBrush.ImageSource = cover;
                } else {
                    _coverBrush.ImageSource = BodianEngine.DefaultCover;
                }
                UpdateProgressUI();
            });
        }

        public void UpdateCover(ImageSource cover) {
            Dispatcher.Invoke(() => {
                _coverBrush.ImageSource = cover != null ? cover : BodianEngine.DefaultCover;
            });
        }

        public void UpdatePlayState(bool isPlaying) {
            Dispatcher.Invoke(() => {
                if (_iconPlayPause != null) {
                    _iconPlayPause.Data = isPlaying ? _geomPause : _geomPlay;
                }
                UpdateProgressUI();
            });
        }

        public void ShowNearOverlay(Window overlay) {
            _showTick = Environment.TickCount;
            Program.Log("[Popup] ShowNearOverlay called, tick=" + _showTick);
            ApplyTheme();

            if (_engine != null) {
                ImageSource img = (_engine.CurrentSong != null) 
                    ? _engine.GetCoverImage(_engine.CurrentSong) 
                    : null;
                UpdateSong(_engine.CurrentSong, img);
                UpdatePlayState(_engine.IsPlaying);
            }

            UpdateProgressUI();

            // 对齐
            double coverLeftDips;
            TaskbarOverlayWindow tbOverlay = overlay as TaskbarOverlayWindow;
            if (tbOverlay != null) {
                coverLeftDips = tbOverlay.GetCoverLeftDips();
            } else {
                coverLeftDips = overlay.Left + 6.0;
            }
            double winX = coverLeftDips - PADDING_H;

            bool isPoppingDownwards = (overlay.Top < 100);
            double winY;
            if (isPoppingDownwards) {
                winY = overlay.Top + overlay.Height;
            } else {
                winY = overlay.Top - Height;
            }

            Rect workArea = SystemParameters.WorkArea;
            try {
                IntPtr hMon = Win32.MonitorFromWindow(new WindowInteropHelper(overlay).Handle, Win32.MONITOR_DEFAULTTOPRIMARY);
                Win32.MONITORINFO mi = new Win32.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(Win32.MONITORINFO));
                if (Win32.GetMonitorInfo(hMon, ref mi)) {
                    double dpiX = 1.0, dpiY = 1.0;
                    PresentationSource src = PresentationSource.FromVisual(overlay);
                    if (src != null && src.CompositionTarget != null) {
                        dpiX = src.CompositionTarget.TransformToDevice.M11;
                        dpiY = src.CompositionTarget.TransformToDevice.M22;
                    }
                    workArea = new Rect(mi.rcWork.Left / dpiX, mi.rcWork.Top / dpiY, mi.rcWork.Width / dpiX, mi.rcWork.Height / dpiY);
                }
            } catch { }

            if (winX + PADDING_H < workArea.Left + 8) winX = workArea.Left + 8 - PADDING_H;
            if (winX + PADDING_H + CARD_WIDTH > workArea.Right - 8) winX = workArea.Right - 8 - CARD_WIDTH - PADDING_H;

            Left = winX;
            Top = winY;
            if (_cardBorder != null) {
                _cardBorder.RenderTransformOrigin = isPoppingDownwards ? new Point(0.0, 0.0) : new Point(0.0, 1.0);
            }

            if (_cardScale != null) {
                _cardScale.ScaleX = 0.94;
                _cardScale.ScaleY = 0.94;
            }
            if (_cardBorder != null) {
                _cardBorder.IsHitTestVisible = true;
                _cardBorder.Opacity = 0.0;
            }

            _isOpen = true;
            Show();
            Activate();
            UpdateLayout();
            UpdateProgressUI();
            PlayEntranceAnimation();
        }

        private void PlayEntranceAnimation() {
            if (_cardBorder == null || _cardScale == null) return;

            CubicEase ease = new CubicEase();
            ease.EasingMode = EasingMode.EaseOut;
            TimeSpan duration = TimeSpan.FromMilliseconds(180);

            DoubleAnimation animOpacity = new DoubleAnimation();
            animOpacity.From = 0.0;
            animOpacity.To = 1.0;
            animOpacity.Duration = duration;
            animOpacity.EasingFunction = ease;
            animOpacity.FillBehavior = FillBehavior.HoldEnd;

            DoubleAnimation animScaleX = new DoubleAnimation();
            animScaleX.From = 0.94;
            animScaleX.To = 1.0;
            animScaleX.Duration = duration;
            animScaleX.EasingFunction = ease;
            animScaleX.FillBehavior = FillBehavior.HoldEnd;

            DoubleAnimation animScaleY = new DoubleAnimation();
            animScaleY.From = 0.94;
            animScaleY.To = 1.0;
            animScaleY.Duration = duration;
            animScaleY.EasingFunction = ease;
            animScaleY.FillBehavior = FillBehavior.HoldEnd;

            _cardBorder.SnapsToDevicePixels = false;

            animScaleY.Completed += (s, e) => {
                if (_cardBorder != null) _cardBorder.SnapsToDevicePixels = true;
                if (_progressTimer != null && IsVisible) _progressTimer.Start();
            };

            _cardBorder.BeginAnimation(UIElement.OpacityProperty, animOpacity);
            _cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            _cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
        }

        private void PlayExitAnimation(Action onCompleted) {
            if (_cardBorder == null || _cardScale == null) {
                if (onCompleted != null) onCompleted();
                return;
            }

            CubicEase ease = new CubicEase();
            ease.EasingMode = EasingMode.EaseIn;
            TimeSpan duration = TimeSpan.FromMilliseconds(140);

            DoubleAnimation animOpacity = new DoubleAnimation();
            animOpacity.To = 0.0;
            animOpacity.Duration = duration;
            animOpacity.EasingFunction = ease;
            animOpacity.FillBehavior = FillBehavior.HoldEnd;

            DoubleAnimation animScaleX = new DoubleAnimation();
            animScaleX.To = 0.94;
            animScaleX.Duration = duration;
            animScaleX.EasingFunction = ease;
            animScaleX.FillBehavior = FillBehavior.HoldEnd;

            DoubleAnimation animScaleY = new DoubleAnimation();
            animScaleY.To = 0.94;
            animScaleY.Duration = duration;
            animScaleY.EasingFunction = ease;
            animScaleY.FillBehavior = FillBehavior.HoldEnd;

            _cardBorder.SnapsToDevicePixels = false;

            bool isFinished = false;
            DispatcherTimer safetyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };

            Action finish = () => {
                if (isFinished) return;
                isFinished = true;
                if (safetyTimer != null) {
                    safetyTimer.Stop();
                    safetyTimer = null;
                }
                if (onCompleted != null) onCompleted();
            };

            safetyTimer.Tick += (s, e) => finish();
            safetyTimer.Start();

            animScaleY.Completed += (s, e) => finish();
            animOpacity.Completed += (s, e) => finish();

            _cardBorder.BeginAnimation(UIElement.OpacityProperty, animOpacity);
            _cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, animScaleX);
            _cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, animScaleY);
        }
    }

}
