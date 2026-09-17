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
using ScrollBar = System.Windows.Controls.Primitives.ScrollBar;
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
    public class SettingsWindow : Window {
        private AppConfig _config;
        private string _configPath;
        private BodianEngine _engine;
        private TaskbarOverlayWindow _overlay;

        // UI 实时状态
        private TextBlock _statusBadgeText;
        private Border _statusBadgeBorder;
        private TextBlock _songTitleText;
        private TextBlock _songArtistText;
        private Ellipse _songCoverEllipse;
        private ImageBrush _songCoverBrush;

        // UI 主题模式与容器引用
        private bool _isDarkTheme = false;
        private Border _windowShadowBorder;
        private Border _outerWindowBorder;
        private Style _lightScrollBarStyle;
        private Style _darkScrollBarStyle;
        private TextBlock _windowTitleText;
        private TextBlock _windowSubTitleText;
        private Border _songCard;
        private Border _tabSidebar;
        private List<TextBlock> _sectionHeaders = new List<TextBlock>();
        private List<Border> _optionCards = new List<Border>();
        private List<Run> _cardTitles = new List<Run>();
        private List<Run> _cardHints = new List<Run>();
        private List<Action> _badgeUpdaters = new List<Action>();
        private Button _btnConfig;
        private Button _btnHide;
        private Button _btnMin;
        private Button _btnClose;

        // 设置控件引用
        private ModernDropdown _cbPositionMode;
        private Slider _slXOffset;
        private TextBlock _tbXOffsetVal;
        private Slider _slYOffset;
        private TextBlock _tbYOffsetVal;
        private Slider _slWidth;
        private TextBlock _tbWidthVal;

        private ModernDropdown _cbAnimType;
        private Slider _slAnimDuration;
        private TextBlock _tbAnimDurationVal;

        private ModernDropdown _cbFontFamily;
        private Slider _slMainFontSize;
        private TextBlock _tbMainFontSizeVal;
        private Slider _slSubFontSize;
        private TextBlock _tbSubFontSizeVal;

        private ModernDropdown _cbUITheme;

        // 胶囊开关
        private CapsuleSwitch _swShowCover;
        private CapsuleSwitch _swRotateCover;
        private CapsuleSwitch _swShowTranslation;
        private CapsuleSwitch _swShadow;
        private CapsuleSwitch _swCardBg;
        private ModernDropdown _cbLyricColorMode;
        private Border _cardCustomColor;
        private Border _swatchCustomColor;
        private TextBox _tbCustomColor;
        private Border _tbCustomColorBorder;
        private CapsuleSwitch _swAutoStart;
        private CapsuleSwitch _swSilentStart;
        private CapsuleSwitch _swRunAsAdmin;
        private CapsuleSwitch _swDisableHardwareAcceleration;

        // 管理员权限与同步状态指示
        private Border _adminStatusCard;
        private TextBlock _adminStatusTitle;
        private TextBlock _adminStatusDesc;
        private Button _btnRestartAdmin;
        private Button _btnExitApp;

        // 底部内联保存提示
        private TextBlock _saveHintText;
        private DispatcherTimer _saveHintTimer;

        // 左侧选项卡导航状态与容器
        private int _currentTabIndex = 0;
        private List<Border> _tabButtons = new List<Border>();
        private List<FrameworkElement> _tabPanels = new List<FrameworkElement>();

        public SettingsWindow(AppConfig config, string configPath, BodianEngine engine, TaskbarOverlayWindow overlay) {
            _config = config;
            _configPath = configPath;
            _engine = engine;
            _overlay = overlay;

            InitSettingsWindow();
            BuildSettingsUI();
            SyncValuesFromConfig();

            // 如果当前已有歌曲信息，立即载入卡片
            if (_engine.CurrentSong != null) {
                _songTitleText.Text = _engine.CurrentSong.Title;
                _songArtistText.Text = string.Format("{0}  ·  {1}", _engine.CurrentSong.Artist, _engine.CurrentSong.Album);
                ImageSource initCover = _engine.GetCoverImage(_engine.CurrentSong);
                _songCoverBrush.ImageSource = initCover != null ? initCover : BodianEngine.DefaultCover;
            } else {
                _songCoverBrush.ImageSource = BodianEngine.DefaultCover;
            }
            UpdateStatusBadge(_engine.IsPlaying);

            _engine.OnSongChanged += (song, cover) => {
                Dispatcher.Invoke(() => {
                    _songTitleText.Text = song.Title;
                    _songArtistText.Text = string.Format("{0}  ·  {1}", song.Artist, song.Album);
                    _songCoverBrush.ImageSource = cover != null ? cover : BodianEngine.DefaultCover;
                });
            };

            _engine.OnCoverChanged += cover => {
                Dispatcher.Invoke(() => {
                    _songCoverBrush.ImageSource = cover != null ? cover : BodianEngine.DefaultCover;
                });
            };

            _engine.OnPlayStateChanged += playing => {
                Dispatcher.Invoke(() => UpdateStatusBadge(playing));
            };

        }

        private void InitSettingsWindow() {
            Title = "波点音乐 - 任务栏歌词控制中心";

            double workW = SystemParameters.WorkArea.Width;
            double workH = SystemParameters.WorkArea.Height;
            Width = Math.Min(800, Math.Max(600, workW - 40));
            Height = Math.Min(670, Math.Max(480, workH - 40));
            MinWidth = Math.Min(720, Math.Max(500, workW - 60));
            MinHeight = Math.Min(600, Math.Max(450, workH - 60));
            MaxHeight = Math.Max(480, workH - 20);

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30));
            FontFamily = new FontFamily("Segoe UI Variable Text, PingFang SC, Microsoft YaHei UI");

            Closing += (s, e) => {
                e.Cancel = true;
                Hide();
            };

            // 每次重新打开窗口时，自动重新同步最新的配置项与状态
            IsVisibleChanged += (s, e) => {
                if (IsVisible) {
                    SyncValuesFromConfig();
                }
                if (_overlay != null) {
                    _overlay.SetSettingsWindowOpen(IsVisible && WindowState != WindowState.Minimized);
                }
            };

            StateChanged += (s, e) => {
                if (_overlay != null) {
                    _overlay.SetSettingsWindowOpen(IsVisible && WindowState != WindowState.Minimized);
                }
            };

            Loaded += (s, e) => {
                var source = PresentationSource.FromVisual(this) as HwndSource;
                if (source != null) {
                    source.AddHook(WndProc);
                }
            };

            InitScrollStyles();
        }

        private void InitScrollStyles() {
            try {
                string lightXaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Style.Triggers>
        <Trigger Property='Orientation' Value='Vertical'>
            <Setter Property='Width' Value='7'/>
            <Setter Property='MinWidth' Value='7'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='ScrollBar'>
                        <Grid Background='Transparent'>
                            <Track x:Name='PART_Track' IsDirectionReversed='True'>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='Thumb'>
                                                <Border x:Name='thumbBorder' CornerRadius='3.5' Background='#35000000' Margin='0,2,0,2'/>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property='IsMouseOver' Value='True'>
                                                        <Setter TargetName='thumbBorder' Property='Background' Value='#65000000'/>
                                                    </Trigger>
                                                    <Trigger Property='IsDragging' Value='True'>
                                                        <Setter TargetName='thumbBorder' Property='Background' Value='#90000000'/>
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Trigger>
    </Style.Triggers>
</Style>";

                string darkXaml = @"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Style.Triggers>
        <Trigger Property='Orientation' Value='Vertical'>
            <Setter Property='Width' Value='7'/>
            <Setter Property='MinWidth' Value='7'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='ScrollBar'>
                        <Grid Background='Transparent'>
                            <Track x:Name='PART_Track' IsDirectionReversed='True'>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='Thumb'>
                                                <Border x:Name='thumbBorder' CornerRadius='3.5' Background='#35FFFFFF' Margin='0,2,0,2'/>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property='IsMouseOver' Value='True'>
                                                        <Setter TargetName='thumbBorder' Property='Background' Value='#65FFFFFF'/>
                                                    </Trigger>
                                                    <Trigger Property='IsDragging' Value='True'>
                                                        <Setter TargetName='thumbBorder' Property='Background' Value='#90FFFFFF'/>
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Trigger>
    </Style.Triggers>
</Style>";

                _lightScrollBarStyle = (Style)XamlReader.Parse(lightXaml);
                _darkScrollBarStyle = (Style)XamlReader.Parse(darkXaml);
            } catch (Exception ex) {
                Program.Log("[SettingsWindow] InitScrollStyles EX: " + ex.Message);
            }
        }

        private ScrollViewer CreatePageScrollViewer(FrameworkElement content) {
            ScrollViewer sv = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Content = content,
                Margin = new Thickness(0, 0, 2, 0)
            };
            ApplyScrollStyleToViewer(sv, _isDarkTheme);
            return sv;
        }

        private void ApplyScrollStyleToViewer(ScrollViewer sv, bool isDark) {
            Style targetStyle = isDark ? _darkScrollBarStyle : _lightScrollBarStyle;
            if (targetStyle != null) {
                sv.Resources[typeof(ScrollBar)] = targetStyle;
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            const int WM_SETTINGCHANGE = 0x001A;
            if (msg == WM_SETTINGCHANGE) {
                if (_config.UITheme == "system") {
                    Dispatcher.Invoke(() => ApplyUITheme("system"));
                }
            }
            return IntPtr.Zero;
        }

        private bool IsSystemDarkMode() {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    if (key != null) {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val != null && (int)val == 0) return true;
                    }
                }
            } catch { }
            return false;
        }

        private void ApplyUITheme(string mode) {
            bool isDark = false;
            if (mode == "light") isDark = false;
            else if (mode == "dark") isDark = true;
            else isDark = IsSystemDarkMode();

            _isDarkTheme = isDark;
            Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));

            if (_windowShadowBorder != null) {
                _windowShadowBorder.Background = isDark ? new SolidColorBrush(Color.FromRgb(24, 24, 26)) : new SolidColorBrush(Color.FromRgb(248, 249, 251));
                DropShadowEffect shadow = _windowShadowBorder.Effect as DropShadowEffect;
                if (shadow != null) {
                    shadow.Opacity = isDark ? 0.35 : 0.16;
                }
            }

            if (_outerWindowBorder != null) {
                _outerWindowBorder.Background = isDark ? new SolidColorBrush(Color.FromRgb(24, 24, 26)) : new SolidColorBrush(Color.FromRgb(248, 249, 251));
                _outerWindowBorder.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(44, 44, 46)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
            }

            if (_windowTitleText != null) {
                _windowTitleText.Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
            }
            if (_windowSubTitleText != null) {
                _windowSubTitleText.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(165, 165, 170)) : new SolidColorBrush(Color.FromRgb(108, 108, 112));
            }

            if (_songCard != null) {
                _songCard.Background = isDark ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White;
                _songCard.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(229, 229, 234));
            }
            if (_songTitleText != null) {
                _songTitleText.Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
            }
            if (_songArtistText != null) {
                _songArtistText.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(175, 175, 180)) : new SolidColorBrush(Color.FromRgb(108, 108, 112));
            }

            if (_tabSidebar != null) {
                _tabSidebar.Background = isDark ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White;
                _tabSidebar.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
            }

            SwitchTab(_currentTabIndex);

            foreach (var sh in _sectionHeaders) {
                sh.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(230, 230, 235)) : new SolidColorBrush(Color.FromRgb(90, 90, 95));
            }

            foreach (var card in _optionCards) {
                card.Background = isDark ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White;
                card.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
            }

            foreach (var r in _cardTitles) {
                r.Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
            }

            foreach (var r in _cardHints) {
                r.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(165, 165, 170)) : new SolidColorBrush(Color.FromRgb(140, 140, 145));
            }

            if (_cbPositionMode != null) _cbPositionMode.ApplyTheme(isDark);

            foreach (var update in _badgeUpdaters) {
                update();
            }

            if (_cbAnimType != null) _cbAnimType.ApplyTheme(isDark);
            if (_cbFontFamily != null) _cbFontFamily.ApplyTheme(isDark);
            if (_cbUITheme != null) _cbUITheme.ApplyTheme(isDark);

            if (_swShowCover != null) _swShowCover.ApplyTheme(isDark);
            if (_swRotateCover != null) _swRotateCover.ApplyTheme(isDark);
            if (_swShadow != null) _swShadow.ApplyTheme(isDark);
            if (_swCardBg != null) _swCardBg.ApplyTheme(isDark);
            if (_cbLyricColorMode != null) _cbLyricColorMode.ApplyTheme(isDark);
            if (_tbCustomColorBorder != null) {
                _tbCustomColorBorder.Background = isDark ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White;
                _tbCustomColorBorder.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(56, 56, 60)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
            }
            if (_tbCustomColor != null) {
                _tbCustomColor.Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
                _tbCustomColor.CaretBrush = isDark ? Brushes.White : Brushes.Black;
            }
            if (_swatchCustomColor != null) {
                _swatchCustomColor.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(80, 80, 85)) : new SolidColorBrush(Color.FromRgb(200, 200, 205));
            }
            if (_swAutoStart != null) _swAutoStart.ApplyTheme(isDark);
            if (_swRunAsAdmin != null) _swRunAsAdmin.ApplyTheme(isDark);
            if (_swDisableHardwareAcceleration != null) _swDisableHardwareAcceleration.ApplyTheme(isDark);

            if (_songCoverEllipse != null) {
                _songCoverEllipse.Stroke = isDark ? new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(25, 0, 0, 0));
            }

            if (_btnConfig != null) {
                _btnConfig.Background = isDark ? new SolidColorBrush(Color.FromRgb(40, 40, 44)) : Brushes.White;
                _btnConfig.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(60, 60, 65)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
                _btnConfig.Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(58, 58, 60));
            }
            if (_btnHide != null) {
                _btnHide.Background = isDark ? new SolidColorBrush(Color.FromRgb(40, 40, 44)) : new SolidColorBrush(Color.FromRgb(242, 242, 247));
                _btnHide.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(60, 60, 65)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
                _btnHide.Foreground = isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
            }
            if (_btnExitApp != null) {
                _btnExitApp.Background = isDark ? new SolidColorBrush(Color.FromRgb(50, 25, 25)) : new SolidColorBrush(Color.FromRgb(254, 242, 242));
                _btnExitApp.BorderBrush = isDark ? new SolidColorBrush(Color.FromRgb(90, 35, 35)) : new SolidColorBrush(Color.FromRgb(252, 165, 165));
            }

            UIHelper.ApplyTitleBarButtonTheme(_btnMin, false, isDark);
            UIHelper.ApplyTitleBarButtonTheme(_btnClose, true, isDark);

            if (_statusBadgeText != null && _engine != null) {
                UpdateStatusBadge(_engine.IsPlaying);
            }
            UpdateAdminStatusCard();

            foreach (var panel in _tabPanels) {
                var sv = panel as ScrollViewer;
                if (sv != null) {
                    ApplyScrollStyleToViewer(sv, isDark);
                }
            }
        }

        private void UpdateStatusBadge(bool playing) {
            if (playing) {
                _statusBadgeText.Text = "🟢  正在播放";
                _statusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(0, 168, 84));
                _statusBadgeBorder.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(232, 248, 238));
                _statusBadgeBorder.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 90, 56)) : new SolidColorBrush(Color.FromRgb(168, 235, 196));
            } else {
                _statusBadgeText.Text = "⏸️  已暂停";
                _statusBadgeText.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                _statusBadgeBorder.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(40, 40, 44)) : new SolidColorBrush(Color.FromRgb(242, 242, 247));
                _statusBadgeBorder.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(60, 60, 65)) : new SolidColorBrush(Color.FromRgb(229, 229, 234));
            }
        }

        private void SwitchTab(int index) {
            _currentTabIndex = index;
            for (int i = 0; i < _tabButtons.Count; i++) {
                bool isSel = (i == index);
                Border btn = _tabButtons[i];
                StackPanel sp = (StackPanel)btn.Child;
                TextBlock icon = (TextBlock)sp.Children[0];
                TextBlock text = (TextBlock)sp.Children[1];

                if (isSel) {
                    btn.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240));
                    btn.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 90, 56)) : new SolidColorBrush(Color.FromRgb(168, 235, 196));
                    text.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(0, 210, 106)) : new SolidColorBrush(Color.FromRgb(0, 168, 84));
                    text.FontWeight = FontWeights.Bold;
                    icon.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(0, 210, 106)) : new SolidColorBrush(Color.FromRgb(0, 168, 84));
                    icon.Opacity = 1.0;
                } else {
                    btn.Background = Brushes.Transparent;
                    btn.BorderBrush = Brushes.Transparent;
                    text.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(210, 210, 215)) : new SolidColorBrush(Color.FromRgb(60, 60, 67));
                    text.FontWeight = FontWeights.SemiBold;
                    icon.Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(70, 70, 75));
                    icon.Opacity = 1.0;
                }

                if (i < _tabPanels.Count) {
                    _tabPanels[i].Visibility = isSel ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private Border CreateTabButton(string iconText, string labelText, int tabIndex) {
            Border tabBtn = new Border {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 11, 14, 11),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent
            };

            StackPanel sp = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock icon = new TextBlock {
                Text = iconText,
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Variable Text"),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(70, 70, 75))
            };
            TextBlock label = new TextBlock {
                Text = labelText,
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(210, 210, 215)) : new SolidColorBrush(Color.FromRgb(60, 60, 67)),
                FontWeight = FontWeights.SemiBold
            };
            sp.Children.Add(icon);
            sp.Children.Add(label);
            tabBtn.Child = sp;

            tabBtn.MouseEnter += (s, e) => {
                if (_currentTabIndex != tabIndex) {
                    tabBtn.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(45, 45, 50)) : new SolidColorBrush(Color.FromRgb(242, 242, 247));
                    icon.Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
                    label.Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
                }
            };
            tabBtn.MouseLeave += (s, e) => {
                if (_currentTabIndex != tabIndex) {
                    tabBtn.Background = Brushes.Transparent;
                    icon.Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(70, 70, 75));
                    label.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(210, 210, 215)) : new SolidColorBrush(Color.FromRgb(60, 60, 67));
                }
            };
            tabBtn.MouseLeftButtonDown += (s, e) => {
                SwitchTab(tabIndex);
            };

            return tabBtn;
        }

        private static string GetFontDisplayName(FontFamily fam, XmlLanguage zhLang) {
            if (fam.FamilyNames.ContainsKey(zhLang)) {
                return string.Format("{0} ({1})", fam.FamilyNames[zhLang], fam.Source);
            }
            return fam.Source;
        }

        private void BuildSettingsUI() {
            _windowShadowBorder = new Border {
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(24, 24, 26)) : new SolidColorBrush(Color.FromRgb(248, 249, 251)),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(14),
                Effect = new DropShadowEffect {
                    BlurRadius = 24,
                    ShadowDepth = 5,
                    Opacity = _isDarkTheme ? 0.35 : 0.16,
                    Color = Colors.Black
                },
                CacheMode = new BitmapCache { RenderAtScale = 1.0 }
            };

            _outerWindowBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(14)
            };

            Grid root = new Grid { Margin = new Thickness(22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Song card
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Options list
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer buttons
            Grid headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 16), Background = Brushes.Transparent };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            headerGrid.MouseLeftButtonDown += (s, e) => {
                if (e.ButtonState == MouseButtonState.Pressed) DragMove();
            };

            StackPanel titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            Border dotLogo = new Border {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(0, 210, 106)),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock logoText = new TextBlock {
                Text = "●",
                FontSize = 14,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dotLogo.Child = logoText;
            titlePanel.Children.Add(dotLogo);

            _windowTitleText = new TextBlock {
                Text = "波点音乐",
                FontSize = 21,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                VerticalAlignment = VerticalAlignment.Center
            };
            _windowSubTitleText = new TextBlock {
                Text = "任务栏歌词控制中心",
                FontSize = 14.5,
                Margin = new Thickness(10, 3, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(108, 108, 112)),
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(_windowTitleText);
            titlePanel.Children.Add(_windowSubTitleText);
            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            _statusBadgeBorder = new Border {
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 5, 12, 5),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusBadgeText = new TextBlock {
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold
            };
            _statusBadgeBorder.Child = _statusBadgeText;
            UpdateStatusBadge(_engine.IsPlaying);
            Grid.SetColumn(_statusBadgeBorder, 1);
            headerGrid.Children.Add(_statusBadgeBorder);

            // 嵌入式最小化与关闭按钮
            StackPanel winControlPanel = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            _btnMin = UIHelper.CreateTitleBarButton("—", () => WindowState = WindowState.Minimized);
            _btnClose = UIHelper.CreateTitleBarButton("✕", () => Hide(), true);
            winControlPanel.Children.Add(_btnMin);
            winControlPanel.Children.Add(_btnClose);
            Grid.SetColumn(winControlPanel, 2);
            headerGrid.Children.Add(winControlPanel);

            Grid.SetRow(headerGrid, 0);
            root.Children.Add(headerGrid);

            // 2. 当前正在播放卡片
            _songCard = new Border {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 229, 234)),
                BorderThickness = new Thickness(1)
            };
            Grid cardGrid = new Grid();
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _songCoverBrush = new ImageBrush {
                Stretch = Stretch.UniformToFill,
                ImageSource = BodianEngine.DefaultCover
            };
            RenderOptions.SetBitmapScalingMode(_songCoverBrush, BitmapScalingMode.HighQuality);

            _songCoverEllipse = new Ellipse {
                Width = 52,
                Height = 52,
                Fill = _songCoverBrush,
                Margin = new Thickness(0, 0, 14, 0)
            };
            Grid.SetColumn(_songCoverEllipse, 0);
            cardGrid.Children.Add(_songCoverEllipse);

            StackPanel infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _songTitleText = new TextBlock {
                Text = _engine.CurrentSong != null ? _engine.CurrentSong.Title : "未检测到播放中的歌曲",
                FontSize = 15.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _songArtistText = new TextBlock {
                Text = _engine.CurrentSong != null ? string.Format("{0}  ·  {1}", _engine.CurrentSong.Artist, _engine.CurrentSong.Album) : "请在波点音乐 PC 端播放歌曲，系统将自动同步",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 108, 112)),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            infoPanel.Children.Add(_songTitleText);
            infoPanel.Children.Add(_songArtistText);
            Grid.SetColumn(infoPanel, 1);
            cardGrid.Children.Add(infoPanel);

            _songCard.Child = cardGrid;
            Grid.SetRow(_songCard, 1);
            root.Children.Add(_songCard);
            Grid mainArea = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            mainArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            mainArea.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 3.1 左侧选项卡导航栏
            _tabSidebar = new Border {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(8, 10, 8, 10),
                Margin = new Thickness(0, 0, 14, 0)
            };
            StackPanel tabList = new StackPanel();
            Border btnTab1 = CreateTabButton("📍", "任务栏位置", 0);
            Border btnTab2 = CreateTabButton("✨", "歌词与动效", 1);
            Border btnTab3 = CreateTabButton("🎨", "封面与外观", 2);
            Border btnTab4 = CreateTabButton("⚙️", "系统与自启", 3);
            Border btnTab5 = CreateTabButton("ℹ️", "关于", 4);
            _tabButtons.Add(btnTab1);
            _tabButtons.Add(btnTab2);
            _tabButtons.Add(btnTab3);
            _tabButtons.Add(btnTab4);
            _tabButtons.Add(btnTab5);
            tabList.Children.Add(btnTab1);
            tabList.Children.Add(btnTab2);
            tabList.Children.Add(btnTab3);
            tabList.Children.Add(btnTab4);
            tabList.Children.Add(btnTab5);
            _tabSidebar.Child = tabList;
            Grid.SetColumn(_tabSidebar, 0);
            mainArea.Children.Add(_tabSidebar);

            // 3.2 右侧分页内容展示区
            Grid contentArea = new Grid();
            StackPanel form1 = new StackPanel();
            form1.Children.Add(CreateSectionHeader("📍 任务栏位置与贴靠微调"));

            _cbPositionMode = new ModernDropdown { Width = 240 };
            _cbPositionMode.Items.Add(new ModernDropdownItem("天气小组件右侧", "weather_right"));
            _cbPositionMode.Items.Add(new ModernDropdownItem("任务栏最左侧", "left"));
            _cbPositionMode.Items.Add(new ModernDropdownItem("任务栏居中", "center"));
            _cbPositionMode.SelectionChanged += (s, e) => {
                if (_cbPositionMode.SelectedItem == null) return;
                _config.PositionMode = _cbPositionMode.SelectedItem.Value;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            form1.Children.Add(CreateOptionCard("任务栏贴靠位置", "", _cbPositionMode));

            _slXOffset = new Slider { Minimum = -50, Maximum = 150, TickFrequency = 1, IsSnapToTickEnabled = true };
            _tbXOffsetVal = new TextBlock();
            _slXOffset.ValueChanged += (s, e) => {
                _tbXOffsetVal.Text = string.Format("{0} px", (int)_slXOffset.Value);
                _config.XOffset = (int)_slXOffset.Value;
                if (_overlay != null) _overlay.UpdateXOffset((int)_slXOffset.Value);
            };
            form1.Children.Add(CreateOptionCard("水平微调偏移", "", CreateSliderControl(_slXOffset, _tbXOffsetVal, 12, "{0} px")));

            _slYOffset = new Slider { Minimum = -25, Maximum = 25, TickFrequency = 1, IsSnapToTickEnabled = true };
            _tbYOffsetVal = new TextBlock();
            _slYOffset.ValueChanged += (s, e) => {
                int val = (int)_slYOffset.Value;
                _tbYOffsetVal.Text = string.Format("{0:+#;-#;0} px", val);
                _config.YOffset = val;
                if (_overlay != null) _overlay.UpdateYOffset(val);
            };
            form1.Children.Add(CreateOptionCard("垂直微调偏移", "", CreateSliderControl(_slYOffset, _tbYOffsetVal, 0, "{0:+#;-#;0} px")));


            _slWidth = new Slider { Minimum = 200, Maximum = 600, TickFrequency = 5, IsSnapToTickEnabled = true };
            _tbWidthVal = new TextBlock();
            _slWidth.ValueChanged += (s, e) => {
                int val = (int)_slWidth.Value;
                _tbWidthVal.Text = string.Format("{0} px", val);
                _config.Width = val;
                if (_overlay != null) _overlay.UpdateWidth(val);
            };
            form1.Children.Add(CreateOptionCard("歌词显示宽度", "", CreateSliderControl(_slWidth, _tbWidthVal, 360, "{0} px")));
            ScrollViewer page1 = CreatePageScrollViewer(form1);

            // Page 2: 歌词动效
            StackPanel form2 = new StackPanel();
            form2.Children.Add(CreateSectionHeader("✨ 歌词动效与过渡"));

            // 转场动效类型
            _cbAnimType = new ModernDropdown { Width = 240 };
            _cbAnimType.Items.Add(new ModernDropdownItem("SlideFade (垂直平滑入 + 淡入淡出)", "SlideFade"));
            _cbAnimType.Items.Add(new ModernDropdownItem("HorizontalSweep (水平扫过)", "HorizontalSweep"));
            _cbAnimType.Items.Add(new ModernDropdownItem("FadeOnly (经典柔和淡入淡出)", "FadeOnly"));
            _cbAnimType.Items.Add(new ModernDropdownItem("None (瞬时切换无动画)", "None"));
            _cbAnimType.SelectionChanged += (s, e) => {
                if (_cbAnimType.SelectedItem == null) return;
                _config.AnimationType = _cbAnimType.SelectedItem.Value;
                _overlay.ApplyConfig(_config);
            };
            form2.Children.Add(CreateOptionCard("歌词过渡类型", "", _cbAnimType));

            // 动效过渡时长 
            _slAnimDuration = new Slider { Minimum = 100, Maximum = 600, TickFrequency = 50, IsSnapToTickEnabled = true };
            _tbAnimDurationVal = new TextBlock();
            _slAnimDuration.ValueChanged += (s, e) => {
                _tbAnimDurationVal.Text = string.Format("{0} ms", (int)_slAnimDuration.Value);
                _config.AnimationDurationMs = (int)_slAnimDuration.Value;
            };
            form2.Children.Add(CreateOptionCard("动效过渡时长", "", CreateSliderControl(_slAnimDuration, _tbAnimDurationVal, 250, "{0} ms")));
            ScrollViewer page2 = CreatePageScrollViewer(form2);

            // Page 3: 封面、外观与个性化
            StackPanel form3 = new StackPanel();
            form3.Children.Add(CreateSectionHeader("🎨 封面、外观与个性化设计"));

            _cbUITheme = new ModernDropdown { Width = 240 };
            _cbUITheme.Items.Add(new ModernDropdownItem("跟随系统 (Auto)", "system"));
            _cbUITheme.Items.Add(new ModernDropdownItem("浅色明亮 (Light)", "light"));
            _cbUITheme.Items.Add(new ModernDropdownItem("深色暗黑 (Dark)", "dark"));
            _cbUITheme.SelectionChanged += (s, e) => {
                if (_cbUITheme.SelectedItem == null) return;
                _config.UITheme = _cbUITheme.SelectedItem.Value;
                ApplyUITheme(_config.UITheme);
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("界面主题", "", _cbUITheme));

            _swShowCover = new CapsuleSwitch();
            _swShowCover.Checked += (s, e) => {
                _config.ShowCover = true;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            _swShowCover.Unchecked += (s, e) => {
                _config.ShowCover = false;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("显示歌曲封面", "", _swShowCover));

            _swRotateCover = new CapsuleSwitch();
            _swRotateCover.Checked += (s, e) => {
                _config.RotateCover = true;
                _overlay.UpdateRotationState();
                _config.Save(_configPath);
            };
            _swRotateCover.Unchecked += (s, e) => {
                _config.RotateCover = false;
                _overlay.UpdateRotationState();
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("黑胶唱片旋转动效", "", _swRotateCover));

            _swShowTranslation = new CapsuleSwitch();
            _swShowTranslation.Checked += (s, e) => {
                _config.ShowTranslation = true;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            _swShowTranslation.Unchecked += (s, e) => {
                _config.ShowTranslation = false;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("显示外文翻译", "在原文歌词下方显示翻译文本", _swShowTranslation));

            _cbFontFamily = new ModernDropdown { Width = 240 };
            var zhLang = XmlLanguage.GetLanguage("zh-cn");
            var recommended = new List<string> {
                "Microsoft YaHei UI",
                "Segoe UI Variable Text",
                "HarmonyOS Sans SC",
                "PingFang SC",
                "DengXian",
                "SimHei",
                "KaiTi",
                "Segoe UI",
                "Arial"
            };

            HashSet<string> addedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. 优先载入常用推荐字体
            foreach (string rec in recommended) {
                foreach (var fam in Fonts.SystemFontFamilies) {
                    if (string.Equals(fam.Source, rec, StringComparison.OrdinalIgnoreCase)) {
                        string disp = GetFontDisplayName(fam, zhLang);
                        _cbFontFamily.Items.Add(new ModernDropdownItem(disp, fam.Source, fam));
                        addedFonts.Add(fam.Source);
                        break;
                    }
                }
            }

            _cbFontFamily.Items.Add(new ModernDropdownItem("─── 全部已安装系统字体 ───", "", null, isHeader: true));

            // 2. 动态载入电脑中全部已安装字体
            var allSysFonts = new List<Tuple<string, string, FontFamily>>();
            foreach (var fam in Fonts.SystemFontFamilies) {
                if (fam.Source.StartsWith("@")) continue;
                if (addedFonts.Contains(fam.Source)) continue;

                string disp = GetFontDisplayName(fam, zhLang);
                allSysFonts.Add(Tuple.Create(disp, fam.Source, fam));
            }

            allSysFonts.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.CurrentCultureIgnoreCase));

            foreach (var item in allSysFonts) {
                _cbFontFamily.Items.Add(new ModernDropdownItem(item.Item1, item.Item2, item.Item3));
            }

            _cbFontFamily.SelectionChanged += (s, e) => {
                if (_cbFontFamily.SelectedItem == null || _cbFontFamily.SelectedItem.IsHeader) return;
                _config.FontFamily = _cbFontFamily.SelectedItem.Value;
                _overlay.ApplyConfig(_config);
            };
            form3.Children.Add(CreateOptionCard("任务栏歌词渲染字体", "", _cbFontFamily));

            // 原文主歌词字号 (默认 15.0, 范围 9.0~22.0)
            _slMainFontSize = new Slider { Minimum = 9.0, Maximum = 22.0, TickFrequency = 0.5, IsSnapToTickEnabled = true };
            _tbMainFontSizeVal = new TextBlock();
            _slMainFontSize.ValueChanged += (s, e) => {
                _tbMainFontSizeVal.Text = string.Format("{0:F1} pt", _slMainFontSize.Value);
                _config.MainFontSize = _slMainFontSize.Value;
                if (_overlay != null) _overlay.UpdateFontSizes(_slMainFontSize.Value, _config.SubFontSize);
            };
            form3.Children.Add(CreateOptionCard("主歌词字号", "", CreateSliderControl(_slMainFontSize, _tbMainFontSizeVal, 15.0, "{0:F1} pt")));

            // 翻译副歌词字号 (默认 12.0, 范围 8.0~18.0)
            _slSubFontSize = new Slider { Minimum = 8.0, Maximum = 18.0, TickFrequency = 0.5, IsSnapToTickEnabled = true };
            _tbSubFontSizeVal = new TextBlock();
            _slSubFontSize.ValueChanged += (s, e) => {
                _tbSubFontSizeVal.Text = string.Format("{0:F1} pt", _slSubFontSize.Value);
                _config.SubFontSize = _slSubFontSize.Value;
                if (_overlay != null) _overlay.UpdateFontSizes(_config.MainFontSize, _slSubFontSize.Value);
            };
            form3.Children.Add(CreateOptionCard("副歌词字号", "外语歌词翻译显示在第二行", CreateSliderControl(_slSubFontSize, _tbSubFontSizeVal, 12.0, "{0:F1} pt")));

            _swShadow = new CapsuleSwitch();
            _swShadow.Checked += (s, e) => {
                _config.ShadowEnabled = true;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            _swShadow.Unchecked += (s, e) => {
                _config.ShadowEnabled = false;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("文字立体微阴影", "避免与桌面壁纸混淆", _swShadow));

            _swCardBg = new CapsuleSwitch();
            _swCardBg.Checked += (s, e) => {
                _config.ShowBackgroundCard = true;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            _swCardBg.Unchecked += (s, e) => {
                _config.ShowBackgroundCard = false;
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("半透明胶囊底衬", "", _swCardBg));

            _cbLyricColorMode = new ModernDropdown { Width = 240 };
            _cbLyricColorMode.Items.Add(new ModernDropdownItem("跟随系统 (Auto)", "system"));
            _cbLyricColorMode.Items.Add(new ModernDropdownItem("经典纯白 (White)", "white"));
            _cbLyricColorMode.Items.Add(new ModernDropdownItem("雅致纯黑 (Black)", "black"));
            _cbLyricColorMode.Items.Add(new ModernDropdownItem("自定义颜色 (Custom RGB)", "custom"));
            _cbLyricColorMode.SelectionChanged += (s, e) => {
                if (_cbLyricColorMode.SelectedItem == null) return;
                string mode = _cbLyricColorMode.SelectedItem.Value;
                _config.ColorMode = mode;
                _config.AutoTheme = (mode == "system");
                if (mode == "custom") {
                    _cardCustomColor.Visibility = Visibility.Visible;
                    Color curC;
                    if (ColorHelper.TryParseColor(_tbCustomColor.Text, out curC)) {
                        _config.CustomColor = _tbCustomColor.Text.Trim();
                        _config.MainTextColor = ColorHelper.ColorToHexString(curC);
                        _swatchCustomColor.Background = new SolidColorBrush(curC);
                    }
                } else {
                    _cardCustomColor.Visibility = Visibility.Collapsed;
                    if (mode == "white") {
                        _config.MainTextColor = "#FFFFFF";
                        _config.SubTextColor = "#C8FFFFFF";
                    } else if (mode == "black") {
                        _config.MainTextColor = "#1E1E1E";
                        _config.SubTextColor = "#C81E1E1E";
                    }
                }
                _overlay.ApplyConfig(_config);
                _config.Save(_configPath);
            };
            form3.Children.Add(CreateOptionCard("自定义字体颜色", "", _cbLyricColorMode));

            StackPanel customInputStack = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _swatchCustomColor = new Border {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                BorderThickness = new Thickness(1.5),
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(80, 80, 85)) : new SolidColorBrush(Color.FromRgb(200, 200, 205)),
                Background = Brushes.White,
                Cursor = Cursors.Hand,
                ToolTip = "点击在调色板中挑选颜色",
                Margin = new Thickness(0, 0, 10, 0)
            };
            _swatchCustomColor.MouseLeftButtonUp += (s, e) => {
                try {
                    using (var dlg = new System.Windows.Forms.ColorDialog()) {
                        dlg.FullOpen = true;
                        Color cur;
                        if (ColorHelper.TryParseColor(_tbCustomColor.Text, out cur)) {
                            dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                        }
                        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                            Color picked = Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                            _tbCustomColor.Text = ColorHelper.ColorToRgbString(picked);
                        }
                    }
                } catch { }
            };

            _tbCustomColorBorder = new Border {
                Width = 140,
                Height = 32,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1.2),
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White,
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(56, 56, 60)) : new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                Padding = new Thickness(8, 0, 8, 0)
            };

            _tbCustomColor = new TextBox {
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 12.5,
                FontFamily = new FontFamily("Consolas, Segoe UI, Microsoft YaHei UI"),
                Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                CaretBrush = _isDarkTheme ? Brushes.White : Brushes.Black,
                Text = _config.CustomColor != null ? _config.CustomColor : "255, 255, 255"
            };

            _tbCustomColor.TextChanged += (s, e) => {
                Color c;
                if (ColorHelper.TryParseColor(_tbCustomColor.Text, out c)) {
                    _swatchCustomColor.Background = new SolidColorBrush(c);
                    _tbCustomColorBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 210, 106));
                    if (string.Equals(_config.ColorMode, "custom", StringComparison.OrdinalIgnoreCase)) {
                        _config.CustomColor = _tbCustomColor.Text.Trim();
                        _config.MainTextColor = ColorHelper.ColorToHexString(c);
                        _overlay.ApplyConfig(_config);
                        _config.Save(_configPath);
                    }
                } else {
                    _tbCustomColorBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                }
            };

            _tbCustomColor.LostFocus += (s, e) => {
                _tbCustomColorBorder.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(56, 56, 60)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
                Color c;
                if (ColorHelper.TryParseColor(_tbCustomColor.Text, out c)) {
                    _tbCustomColor.Text = ColorHelper.ColorToRgbString(c);
                } else {
                    _tbCustomColor.Text = _config.CustomColor != null ? _config.CustomColor : "255, 255, 255";
                }
            };

            _tbCustomColorBorder.Child = _tbCustomColor;

            customInputStack.Children.Add(_swatchCustomColor);
            customInputStack.Children.Add(_tbCustomColorBorder);

            _cardCustomColor = CreateOptionCard("自定义RGB颜色", "可点击色块选取", customInputStack);
            _cardCustomColor.Visibility = string.Equals(_config.ColorMode, "custom", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            form3.Children.Add(_cardCustomColor);
            ScrollViewer page3 = CreatePageScrollViewer(form3);

            // Page 4
            StackPanel form4 = new StackPanel();
            form4.Children.Add(CreateSectionHeader("⚙️ 系统级联动与开机项"));

            _swAutoStart = new CapsuleSwitch();
            _swAutoStart.Checked += (s, e) => Win32.SetAutoStart(true);
            _swAutoStart.Unchecked += (s, e) => Win32.SetAutoStart(false);
            form4.Children.Add(CreateOptionCard("开机自动启动", "", _swAutoStart));

            _swSilentStart = new CapsuleSwitch();
            _swSilentStart.Checked += (s, e) => {
                _config.SilentStart = true;
                _config.Save(_configPath);
            };
            _swSilentStart.Unchecked += (s, e) => {
                _config.SilentStart = false;
                _config.Save(_configPath);
            };
            form4.Children.Add(CreateOptionCard("启动时静默", "", _swSilentStart));

            _swRunAsAdmin = new CapsuleSwitch();
            _swRunAsAdmin.Checked += (s, e) => {
                _config.RunAsAdmin = true;
                _config.Save(_configPath);
                UpdateAdminStatusCard();
            };
            _swRunAsAdmin.Unchecked += (s, e) => {
                _config.RunAsAdmin = false;
                _config.Save(_configPath);
                UpdateAdminStatusCard();
            };
            form4.Children.Add(CreateOptionCard("以管理员权限运行(推荐)", "避免播放器提权后进度不同步", _swRunAsAdmin));

            _swDisableHardwareAcceleration = new CapsuleSwitch();
            _swDisableHardwareAcceleration.Checked += (s, e) => {
                _config.DisableHardwareAcceleration = true;
                _config.Save(_configPath);
            };
            _swDisableHardwareAcceleration.Unchecked += (s, e) => {
                _config.DisableHardwareAcceleration = false;
                _config.Save(_configPath);
            };
            form4.Children.Add(CreateOptionCard("禁用GPU硬件加速", "关闭无法切换至独显直连模式", _swDisableHardwareAcceleration));

            form4.Children.Add(CreateAdminStatusCard());

            _btnExitApp = UIHelper.CreateRoundedButton("❌ 退出任务栏歌词程序",
                _isDarkTheme ? new SolidColorBrush(Color.FromRgb(50, 25, 25)) : new SolidColorBrush(Color.FromRgb(254, 242, 242)),
                _isDarkTheme ? new SolidColorBrush(Color.FromRgb(70, 30, 30)) : new SolidColorBrush(Color.FromRgb(254, 226, 226)),
                new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                _isDarkTheme ? new SolidColorBrush(Color.FromRgb(90, 35, 35)) : new SolidColorBrush(Color.FromRgb(252, 165, 165)),
                1.0, 10, new Thickness(16, 8, 16, 8));
            _btnExitApp.Margin = new Thickness(0, 14, 0, 0);
            _btnExitApp.HorizontalAlignment = HorizontalAlignment.Left;
            _btnExitApp.Click += (s, e) => {
                if (MessageBox.Show("确定要完全退出波点音乐任务栏歌词程序吗？", "退出确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) {
                    Application.Current.Shutdown();
                }
            };
            form4.Children.Add(_btnExitApp);

            ScrollViewer page4 = CreatePageScrollViewer(form4);

            // Page 5
            StackPanel form5 = new StackPanel();
            form5.Children.Add(CreateSectionHeader("ℹ️ 关于软件与开源项目"));

            // 1. 版本号展示卡片
            Border versionBadge = new Border {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 5, 14, 5),
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240)),
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 90, 56)) : new SolidColorBrush(Color.FromRgb(168, 235, 196)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock tbVersion = new TextBlock {
                Text = "v" + AppConfig.APP_VERSION,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(0, 210, 106)) : new SolidColorBrush(Color.FromRgb(0, 168, 84))
            };
            versionBadge.Child = tbVersion;
            _badgeUpdaters.Add(() => {
                versionBadge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240));
                versionBadge.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 90, 56)) : new SolidColorBrush(Color.FromRgb(168, 235, 196));
                tbVersion.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(0, 210, 106)) : new SolidColorBrush(Color.FromRgb(0, 168, 84));
            });
            form5.Children.Add(CreateOptionCard("当前版本", "波点音乐专属任务栏歌词", versionBadge));

            // 2. 开源地址卡片
            Border repoCard = new Border {
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White,
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 9)
            };
            _optionCards.Add(repoCard);

            StackPanel spRepo = new StackPanel();

            Grid repoHeader = new Grid();
            repoHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            repoHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock tbRepoHeader = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Run rRepoTitle = new Run("开源代码仓库") {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30))
            };
            _cardTitles.Add(rRepoTitle);
            Run rRepoHint = new Run("  GitHub 官方仓库") {
                FontSize = 11.0,
                FontWeight = FontWeights.Normal,
                Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(124, 124, 128)) : new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            _cardHints.Add(rRepoHint);
            tbRepoHeader.Inlines.Add(rRepoTitle);
            tbRepoHeader.Inlines.Add(rRepoHint);
            Grid.SetColumn(tbRepoHeader, 0);
            repoHeader.Children.Add(tbRepoHeader);

            TextBlock tbCopyHint = new TextBlock {
                Text = "✓ 已复制到剪贴板",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 168, 84)),
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.0
            };
            Grid.SetColumn(tbCopyHint, 1);
            repoHeader.Children.Add(tbCopyHint);
            spRepo.Children.Add(repoHeader);

            // 链接展示框 (可点击在浏览器打开)
            const string repoUrl = "https://github.com/weiwei221206/BodianTaskbarLyric";
            Border urlContainer = new Border {
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(28, 28, 30)) : new SolidColorBrush(Color.FromRgb(245, 246, 248)),
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(50, 50, 54)) : new SolidColorBrush(Color.FromRgb(230, 232, 238)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 9, 12, 9),
                Margin = new Thickness(0, 10, 0, 10),
                Cursor = Cursors.Hand,
                ToolTip = "点击在浏览器中打开开源项目主页"
            };
            TextBlock tbUrl = new TextBlock {
                Text = repoUrl,
                FontFamily = new FontFamily("Consolas, Segoe UI, Microsoft YaHei UI"),
                FontSize = 12.5,
                Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(0, 210, 106)) : new SolidColorBrush(Color.FromRgb(0, 168, 84)),
                TextDecorations = TextDecorations.Underline,
                VerticalAlignment = VerticalAlignment.Center
            };
            urlContainer.Child = tbUrl;
            urlContainer.MouseLeftButtonDown += (s, e) => {
                try { Process.Start(repoUrl); } catch { }
            };
            spRepo.Children.Add(urlContainer);
            StackPanel repoBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

            Button btnOpenRepo = UIHelper.CreateRoundedButton("🌐 访问仓库",
                new SolidColorBrush(Color.FromRgb(0, 210, 106)),
                new SolidColorBrush(Color.FromRgb(0, 186, 94)),
                Brushes.White,
                null, 0, 10, new Thickness(16, 7, 16, 7), true);
            btnOpenRepo.Margin = new Thickness(0, 0, 10, 0);
            btnOpenRepo.Click += (s, e) => {
                try { Process.Start(repoUrl); } catch { }
            };

            Button btnCopyRepo = UIHelper.CreateRoundedButton("📋 复制链接",
                _isDarkTheme ? new SolidColorBrush(Color.FromRgb(48, 48, 52)) : new SolidColorBrush(Color.FromRgb(242, 242, 247)),
                _isDarkTheme ? new SolidColorBrush(Color.FromRgb(60, 60, 65)) : new SolidColorBrush(Color.FromRgb(230, 230, 235)),
                _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(58, 58, 60)),
                _isDarkTheme ? new SolidColorBrush(Color.FromRgb(64, 64, 68)) : new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                1.0, 10, new Thickness(16, 7, 16, 7));

            DispatcherTimer copyHintTimer = null;
            btnCopyRepo.Click += (s, e) => {
                try {
                    System.Windows.Clipboard.SetText(repoUrl);
                    tbCopyHint.BeginAnimation(UIElement.OpacityProperty, null);
                    tbCopyHint.Opacity = 1.0;
                    if (copyHintTimer == null) {
                        copyHintTimer = new DispatcherTimer();
                        copyHintTimer.Interval = TimeSpan.FromSeconds(2.0);
                        copyHintTimer.Tick += (ts, te) => {
                            copyHintTimer.Stop();
                            DoubleAnimation fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
                            tbCopyHint.BeginAnimation(UIElement.OpacityProperty, fade);
                        };
                    }
                    copyHintTimer.Stop();
                    copyHintTimer.Start();
                } catch { }
            };

            repoBtnRow.Children.Add(btnOpenRepo);
            repoBtnRow.Children.Add(btnCopyRepo);
            spRepo.Children.Add(repoBtnRow);
            repoCard.Child = spRepo;
            form5.Children.Add(repoCard);

            _badgeUpdaters.Add(() => {
                urlContainer.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(28, 28, 30)) : new SolidColorBrush(Color.FromRgb(245, 246, 248));
                urlContainer.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(50, 50, 54)) : new SolidColorBrush(Color.FromRgb(230, 232, 238));
                tbUrl.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(0, 210, 106)) : new SolidColorBrush(Color.FromRgb(0, 168, 84));
                btnCopyRepo.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(48, 48, 52)) : new SolidColorBrush(Color.FromRgb(242, 242, 247));
                btnCopyRepo.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(64, 64, 68)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
                btnCopyRepo.Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(58, 58, 60));
            });

            // 3. 项目特性与介绍卡片
            Border introCard = new Border {
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White,
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 9)
            };
            _optionCards.Add(introCard);

            StackPanel spIntro = new StackPanel();
            Run rIntroTitle = new Run("关于项目") {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30))
            };
            _cardTitles.Add(rIntroTitle);
            TextBlock tbIntroTitle = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };
            tbIntroTitle.Inlines.Add(rIntroTitle);
            spIntro.Children.Add(tbIntroTitle);

            TextBlock tbIntroBody = new TextBlock {
                Text = "波点音乐任务栏歌词是一款轻量、极简且流畅的 Windows 任务栏歌词工具。支持波点音乐 PC 端播放状态与进度的实时同步、专辑封面黑胶动效、外文双行翻译以及高度个性化外观定制。",
                FontSize = 12,
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(180, 180, 185)) : new SolidColorBrush(Color.FromRgb(108, 108, 112))
            };
            _badgeUpdaters.Add(() => {
                tbIntroBody.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(180, 180, 185)) : new SolidColorBrush(Color.FromRgb(108, 108, 112));
            });
            spIntro.Children.Add(tbIntroBody);
            introCard.Child = spIntro;
            form5.Children.Add(introCard);

            // 4. 免责与版权声明卡片
            Border disclaimCard = new Border {
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White,
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 0, 0, 9)
            };
            _optionCards.Add(disclaimCard);

            StackPanel spDisclaim = new StackPanel();
            Run rDisclaimTitle = new Run("免责声明") {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30))
            };
            _cardTitles.Add(rDisclaimTitle);
            TextBlock tbDisclaimTitle = new TextBlock { Margin = new Thickness(0, 0, 0, 6) };
            tbDisclaimTitle.Inlines.Add(rDisclaimTitle);
            spDisclaim.Children.Add(tbDisclaimTitle);

            TextBlock tbDisclaimBody = new TextBlock {
                Text = "本项目开源且免费，仅供个人技术交流与学习使用。软件中涉及的“波点音乐”名称、图标及音乐相关版权均归其合法版权方所有。",
                FontSize = 12,
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(180, 180, 185)) : new SolidColorBrush(Color.FromRgb(108, 108, 112))
            };
            _badgeUpdaters.Add(() => {
                tbDisclaimBody.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(180, 180, 185)) : new SolidColorBrush(Color.FromRgb(108, 108, 112));
            });
            spDisclaim.Children.Add(tbDisclaimBody);
            disclaimCard.Child = spDisclaim;
            form5.Children.Add(disclaimCard);

            ScrollViewer page5 = CreatePageScrollViewer(form5);

            _tabPanels.Add(page1);
            _tabPanels.Add(page2);
            _tabPanels.Add(page3);
            _tabPanels.Add(page4);
            _tabPanels.Add(page5);

            contentArea.Children.Add(page1);
            contentArea.Children.Add(page2);
            contentArea.Children.Add(page3);
            contentArea.Children.Add(page4);
            contentArea.Children.Add(page5);

            Grid.SetColumn(contentArea, 1);
            mainArea.Children.Add(contentArea);

            Grid.SetRow(mainArea, 2);
            root.Children.Add(mainArea);

            // 初始激活第一个选项卡
            SwitchTab(0);

            // 4. 底部操作栏
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _btnConfig = UIHelper.CreateRoundedButton("⚙️ 打开 config.json",
                Brushes.White,
                new SolidColorBrush(Color.FromRgb(242, 242, 247)),
                new SolidColorBrush(Color.FromRgb(58, 58, 60)),
                new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                1.0, 10, new Thickness(16, 8, 16, 8));
            _btnConfig.Click += (s, e) => {
                try { Process.Start("notepad.exe", _configPath); } catch { }
            };
            Grid.SetColumn(_btnConfig, 0);
            footer.Children.Add(_btnConfig);

            StackPanel rightBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _saveHintText = new TextBlock {
                Text = "✓ 已保存并应用",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 168, 84)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Opacity = 0.0
            };
            rightBtns.Children.Add(_saveHintText);

            Button btnSave = UIHelper.CreateRoundedButton("保存并应用",
                new SolidColorBrush(Color.FromRgb(0, 210, 106)),
                new SolidColorBrush(Color.FromRgb(0, 186, 94)),
                Brushes.White,
                null, 0, 10, new Thickness(24, 8, 24, 8), true);
            btnSave.Margin = new Thickness(0, 0, 10, 0);
            btnSave.Effect = new DropShadowEffect {
                BlurRadius = 8,
                ShadowDepth = 2,
                Opacity = 0.22,
                Color = Color.FromRgb(0, 210, 106)
            };
            btnSave.CacheMode = new BitmapCache { RenderAtScale = 1.0 };
            btnSave.Click += (s, e) => {
                SaveValuesToConfig();
                _overlay.ApplyConfig(_config);
                _saveHintText.BeginAnimation(UIElement.OpacityProperty, null);
                _saveHintText.Opacity = 0.0;
                DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                _saveHintText.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                if (_saveHintTimer == null) {
                    _saveHintTimer = new DispatcherTimer();
                    _saveHintTimer.Tick += (st, se) => {
                        _saveHintTimer.Stop();
                        DoubleAnimation fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
                        _saveHintText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    };
                }
                _saveHintTimer.Interval = TimeSpan.FromSeconds(2.5);
                _saveHintTimer.Stop();
                _saveHintTimer.Start();
            };

            _btnHide = UIHelper.CreateRoundedButton("隐藏到托盘",
                new SolidColorBrush(Color.FromRgb(242, 242, 247)),
                new SolidColorBrush(Color.FromRgb(230, 230, 235)),
                new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                1.0, 10, new Thickness(18, 8, 18, 8));
            _btnHide.Click += (s, e) => Hide();

            rightBtns.Children.Add(btnSave);
            rightBtns.Children.Add(_btnHide);
            Grid.SetColumn(rightBtns, 2);
            footer.Children.Add(rightBtns);

            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            _outerWindowBorder.Child = root;

            Grid windowRoot = new Grid();
            windowRoot.Children.Add(_windowShadowBorder);
            windowRoot.Children.Add(_outerWindowBorder);
            Content = windowRoot;
        }

        private TextBlock CreateSectionHeader(string title) {
            TextBlock tb = new TextBlock {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 108, 112)),
                Margin = new Thickness(4, 12, 0, 6)
            };
            _sectionHeaders.Add(tb);
            return tb;
        }

        private Border CreateOptionCard(string mainTitle, string parenthesisHint, FrameworkElement control) {
            Border card = new Border {
                Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White,
                BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(18), // 椭圆大圆角胶囊边框
                Padding = new Thickness(18, 12, 18, 12),
                Margin = new Thickness(0, 0, 0, 9)
            };
            _optionCards.Add(card);

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock tb = new TextBlock {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Run rTitle = new Run(mainTitle) {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30))
            };
            _cardTitles.Add(rTitle);
            tb.Inlines.Add(rTitle);

            if (!string.IsNullOrEmpty(parenthesisHint)) {
                Run rHint = new Run("  " + parenthesisHint) {
                    FontSize = 11.0, // 副标题提示专用小字
                    FontWeight = FontWeights.Normal,
                    Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(124, 124, 128)) : new SolidColorBrush(Color.FromRgb(142, 142, 147))
                };
                _cardHints.Add(rHint);
                tb.Inlines.Add(rHint);
            }

            control.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(tb, 0);
            Grid.SetColumn(control, 1);
            g.Children.Add(tb);
            g.Children.Add(control);

            card.Child = g;

            if (control is CapsuleSwitch) {
                card.Cursor = Cursors.Hand;
                card.MouseLeftButtonDown += (s, e) => {
                    CapsuleSwitch sw = (CapsuleSwitch)control;
                    sw.IsChecked = !sw.IsChecked;
                    e.Handled = true;
                };
            }

            return card;
        }

        private Border CreateAdminStatusCard() {
            _adminStatusCard = new Border {
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 4, 0, 9)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel spText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _adminStatusTitle = new TextBlock {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold
            };
            _adminStatusDesc = new TextBlock {
                FontSize = 11.5,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            spText.Children.Add(_adminStatusTitle);
            spText.Children.Add(_adminStatusDesc);
            Grid.SetColumn(spText, 0);
            g.Children.Add(spText);

            _btnRestartAdmin = UIHelper.CreateRoundedButton("🛡️ 立即以管理员重启",
                new SolidColorBrush(Color.FromRgb(255, 149, 0)),
                new SolidColorBrush(Color.FromRgb(230, 130, 0)),
                Brushes.White,
                null, 0, 10, new Thickness(14, 6, 14, 6), true);
            _btnRestartAdmin.VerticalAlignment = VerticalAlignment.Center;
            _btnRestartAdmin.Margin = new Thickness(12, 0, 0, 0);
            _btnRestartAdmin.Click += (s, e) => {
                Win32.RestartElevated();
            };
            Grid.SetColumn(_btnRestartAdmin, 1);
            g.Children.Add(_btnRestartAdmin);

            _adminStatusCard.Child = g;
            UpdateAdminStatusCard();
            return _adminStatusCard;
        }

        private void UpdateAdminStatusCard() {
            if (_adminStatusCard == null || _adminStatusTitle == null || _adminStatusDesc == null) return;
            bool isAdmin = Win32.IsAdministrator();
            bool isDenied = _engine != null && _engine.IsMemoryAccessDenied;

            if (isAdmin) {
                _adminStatusCard.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240));
                _adminStatusCard.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 90, 56)) : new SolidColorBrush(Color.FromRgb(168, 235, 196));
                _adminStatusTitle.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(50, 230, 140)) : new SolidColorBrush(Color.FromRgb(0, 168, 84));
                _adminStatusTitle.Text = "🛡️ 当前已拥有管理员权限 (提权成功)";
                _adminStatusDesc.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(180, 230, 200)) : new SolidColorBrush(Color.FromRgb(40, 120, 70));
                _adminStatusDesc.Text = "已具备跨进程内存读取权限。波点音乐播放、起播或切歌时，任务栏歌词将实时精准对齐。";
                if (_btnRestartAdmin != null) _btnRestartAdmin.Visibility = Visibility.Collapsed;
            } else if (isDenied) {
                _adminStatusCard.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(58, 38, 18)) : new SolidColorBrush(Color.FromRgb(255, 243, 230));
                _adminStatusCard.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(120, 80, 25)) : new SolidColorBrush(Color.FromRgb(255, 200, 140));
                _adminStatusTitle.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(255, 180, 50)) : new SolidColorBrush(Color.FromRgb(255, 149, 0));
                _adminStatusTitle.Text = "⚠️ 权限受限 (波点音乐正以管理员权限运行)";
                _adminStatusDesc.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(240, 210, 180)) : new SolidColorBrush(Color.FromRgb(150, 90, 20));
                _adminStatusDesc.Text = "当前软件为标准权限，受 Windows 安全隔离限制可能无法直读播放进度。请点击右侧按钮提权。";
                if (_btnRestartAdmin != null) _btnRestartAdmin.Visibility = Visibility.Visible;
            } else {
                _adminStatusCard.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(36, 36, 38)) : Brushes.White;
                _adminStatusCard.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(52, 52, 56)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
                _adminStatusTitle.Foreground = _isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30));
                _adminStatusTitle.Text = "ℹ️ 当前以标准用户权限运行";
                _adminStatusDesc.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(165, 165, 170)) : new SolidColorBrush(Color.FromRgb(108, 108, 112));
                _adminStatusDesc.Text = "若波点音乐由安装包启动或已提权，建议开启上方“默认以管理员权限运行”开关并提权重启。";
                if (_btnRestartAdmin != null) _btnRestartAdmin.Visibility = Visibility.Visible;
            }
        }

        private FrameworkElement CreateSliderControl(Slider slider, TextBlock valText, double defaultVal, string formatString) {
            StackPanel sp = new StackPanel {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid sliderContainer = new Grid {
                Width = 180,
                Height = 32,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            slider.Width = 180;
            slider.VerticalAlignment = VerticalAlignment.Center;

            double ratio = (defaultVal - slider.Minimum) / (slider.Maximum - slider.Minimum);
            double cx = 5.5 + ratio * (180.0 - 11.0);

            // 默认值指示圆点
            Ellipse dot = new Ellipse {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0, 210, 106)),
                Stroke = new SolidColorBrush(Color.FromRgb(0, 168, 84)),
                StrokeThickness = 0.8,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(Math.Round(cx - 3), 0, 0, 3),
                Cursor = Cursors.Hand
            };
            dot.MouseLeftButtonDown += (s, e) => {
                slider.Value = defaultVal;
                e.Handled = true;
            };

            // 微刻度垂直指向标
            Rectangle tick = new Rectangle {
                Width = 1.5,
                Height = 4,
                Fill = new SolidColorBrush(Color.FromRgb(0, 210, 106)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(Math.Round(cx - 0.75), 0, 0, 8),
                IsHitTestVisible = false
            };

            sliderContainer.Children.Add(tick);
            sliderContainer.Children.Add(dot);
            sliderContainer.Children.Add(slider);

            Border badge = new Border {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 8, 3),
                MinWidth = 54,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            valText.FontSize = 11.5;
            valText.FontWeight = FontWeights.Medium;
            valText.TextAlignment = TextAlignment.Center;
            badge.Child = valText;

            int lastDefState = -1;
            Action updateBadgeStyle = () => {
                bool isDef = Math.Abs(slider.Value - defaultVal) < 0.01;
                int curState = isDef ? 1 : 0;
                if (curState == lastDefState) return;
                lastDefState = curState;

                if (isDef) {
                    badge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240));
                    badge.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(31, 90, 56)) : new SolidColorBrush(Color.FromRgb(168, 235, 196));
                    valText.Foreground = new SolidColorBrush(Color.FromRgb(0, 168, 84));
                } else {
                    badge.Background = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(50, 50, 54)) : new SolidColorBrush(Color.FromRgb(242, 242, 247));
                    badge.BorderBrush = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(64, 64, 68)) : new SolidColorBrush(Color.FromRgb(226, 228, 233));
                    valText.Foreground = _isDarkTheme ? new SolidColorBrush(Color.FromRgb(230, 230, 235)) : new SolidColorBrush(Color.FromRgb(58, 58, 60));
                }
            };

            _badgeUpdaters.Add(() => {
                lastDefState = -1;
                updateBadgeStyle();
            });

            slider.ValueChanged += (s, e) => {
                updateBadgeStyle();
            };

            badge.MouseLeftButtonDown += (s, e) => {
                slider.Value = defaultVal;
            };

            updateBadgeStyle();

            sp.Children.Add(sliderContainer);
            sp.Children.Add(badge);
            return sp;
        }

        private void SyncValuesFromConfig() {
            if (_cbPositionMode != null) {
                int posIdx = 0;
                for (int i = 0; i < _cbPositionMode.Items.Count; i++) {
                    if (string.Equals(_cbPositionMode.Items[i].Value, _config.PositionMode, StringComparison.OrdinalIgnoreCase)) {
                        posIdx = i;
                        break;
                    }
                }
                _cbPositionMode.SelectedIndex = posIdx;
            }

            _slXOffset.Value = _config.XOffset;
            _tbXOffsetVal.Text = string.Format("{0} px", _config.XOffset);

            _slYOffset.Value = _config.YOffset;
            _tbYOffsetVal.Text = string.Format("{0:+#;-#;0} px", _config.YOffset);

            _slWidth.Value = _config.Width;
            _tbWidthVal.Text = string.Format("{0} px", _config.Width);

            if (_config.AnimationType == "HorizontalSweep") _cbAnimType.SelectedIndex = 1;
            else if (_config.AnimationType == "FadeOnly") _cbAnimType.SelectedIndex = 2;
            else if (_config.AnimationType == "None") _cbAnimType.SelectedIndex = 3;
            else _cbAnimType.SelectedIndex = 0;

            _slAnimDuration.Value = _config.AnimationDurationMs;
            _tbAnimDurationVal.Text = string.Format("{0} ms", _config.AnimationDurationMs);

            int fIdx = 0;
            for (int i = 0; i < _cbFontFamily.Items.Count; i++) {
                var item = _cbFontFamily.Items[i];
                if (!item.IsHeader && string.Equals(item.Value, _config.FontFamily, StringComparison.OrdinalIgnoreCase)) {
                    fIdx = i; break;
                }
            }
            _cbFontFamily.SelectedIndex = fIdx;

            _slMainFontSize.Value = _config.MainFontSize;
            _tbMainFontSizeVal.Text = string.Format("{0:F1} pt", _config.MainFontSize);

            _slSubFontSize.Value = _config.SubFontSize;
            _tbSubFontSizeVal.Text = string.Format("{0:F1} pt", _config.SubFontSize);

            if (_config.UITheme == "light") _cbUITheme.SelectedIndex = 1;
            else if (_config.UITheme == "dark") _cbUITheme.SelectedIndex = 2;
            else _cbUITheme.SelectedIndex = 0;

            _swShowCover.SetChecked(_config.ShowCover, false);
            _swRotateCover.SetChecked(_config.RotateCover, false);
            _swShowTranslation.SetChecked(_config.ShowTranslation, false);
            _swShadow.SetChecked(_config.ShadowEnabled, false);
            _swCardBg.SetChecked(_config.ShowBackgroundCard, false);

            if (string.Equals(_config.ColorMode, "white", StringComparison.OrdinalIgnoreCase)) _cbLyricColorMode.SelectedIndex = 1;
            else if (string.Equals(_config.ColorMode, "black", StringComparison.OrdinalIgnoreCase)) _cbLyricColorMode.SelectedIndex = 2;
            else if (string.Equals(_config.ColorMode, "custom", StringComparison.OrdinalIgnoreCase)) _cbLyricColorMode.SelectedIndex = 3;
            else _cbLyricColorMode.SelectedIndex = 0;

            bool isCustom = string.Equals(_config.ColorMode, "custom", StringComparison.OrdinalIgnoreCase);
            if (_cardCustomColor != null) _cardCustomColor.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (_tbCustomColor != null) {
                Color c;
                if (ColorHelper.TryParseColor(_config.CustomColor, out c)) {
                    _tbCustomColor.Text = ColorHelper.ColorToRgbString(c);
                    if (_swatchCustomColor != null) _swatchCustomColor.Background = new SolidColorBrush(c);
                } else {
                    _tbCustomColor.Text = !string.IsNullOrEmpty(_config.CustomColor) ? _config.CustomColor : "255, 255, 255";
                }
            }

            _swAutoStart.SetChecked(Win32.IsAutoStartEnabled(), false);
            _swSilentStart.SetChecked(_config.SilentStart, false);
            _swRunAsAdmin.SetChecked(_config.RunAsAdmin, false);
            _swDisableHardwareAcceleration.SetChecked(_config.DisableHardwareAcceleration, false);
            UpdateAdminStatusCard();

            ApplyUITheme(_config.UITheme);
        }

        private void SaveValuesToConfig() {
            if (_cbPositionMode != null && _cbPositionMode.SelectedItem != null) {
                _config.PositionMode = _cbPositionMode.SelectedItem.Value;
            }
            _config.XOffset = (int)_slXOffset.Value;
            _config.YOffset = (int)_slYOffset.Value;
            _config.Width = (int)_slWidth.Value;

            if (_cbAnimType.SelectedItem != null) {
                _config.AnimationType = _cbAnimType.SelectedItem.Value;
            }

            _config.AnimationDurationMs = (int)_slAnimDuration.Value;

            if (_cbFontFamily.SelectedItem != null && !_cbFontFamily.SelectedItem.IsHeader) {
                _config.FontFamily = _cbFontFamily.SelectedItem.Value;
            }

            _config.MainFontSize = _slMainFontSize.Value;
            _config.SubFontSize = _slSubFontSize.Value;

            if (_cbUITheme.SelectedItem != null) {
                _config.UITheme = _cbUITheme.SelectedItem.Value;
            }

            if (_cbLyricColorMode != null && _cbLyricColorMode.SelectedItem != null) {
                _config.ColorMode = _cbLyricColorMode.SelectedItem.Value;
                _config.AutoTheme = (_config.ColorMode == "system");
            }
            if (_tbCustomColor != null) {
                _config.CustomColor = _tbCustomColor.Text.Trim();
                Color c;
                if (ColorHelper.TryParseColor(_config.CustomColor, out c)) {
                    _config.MainTextColor = ColorHelper.ColorToHexString(c);
                }
            }

            _config.ShowCover = (_swShowCover.IsChecked == true);
            _config.RotateCover = (_swRotateCover.IsChecked == true);
            _config.ShowTranslation = (_swShowTranslation.IsChecked == true);
            _config.ShadowEnabled = (_swShadow.IsChecked == true);
            _config.ShowBackgroundCard = (_swCardBg.IsChecked == true);
            _config.SilentStart = (_swSilentStart.IsChecked == true);
            _config.RunAsAdmin = (_swRunAsAdmin.IsChecked == true);
            _config.DisableHardwareAcceleration = (_swDisableHardwareAcceleration.IsChecked == true);

            Win32.SetAutoStart(_swAutoStart.IsChecked == true);
            _config.Save(_configPath);
        }
    }

}
