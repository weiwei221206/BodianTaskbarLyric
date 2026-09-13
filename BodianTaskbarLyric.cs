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
using System.Windows.Forms; // NotifyIcon
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

[assembly: AssemblyTitle("波点音乐专属任务栏歌词")]
[assembly: AssemblyProduct("BodianTaskbarLyric")]
[assembly: AssemblyVersion("1.0.3.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]
[assembly: AssemblyInformationalVersion("1.0.3")]

namespace BodianTaskbarLyric {

    // ==========================================
    // 1. 配置数据模型 (Config Model)
    // ==========================================
    public class AppConfig {
        public const string APP_VERSION = "1.0.3";
        public string Version = APP_VERSION;

        public string PositionMode = "left"; // "weather_right", "left" 或 "center"
        public int XOffset = 12;
        public int YOffset = 0; // 垂直微调偏移 (负数偏上，正数偏下)
        public int Width = 360;
        public int Height = 48;
        public bool ShowCover = true;
        public bool RotateCover = true; // 封面黑胶旋转动效开关
        public int CoverSize = 28;
        public int CoverRadius = 14;

        public string AnimationType = "HorizontalSweep"; // "HorizontalSweep", "SlideFade", "FadeOnly", "None"
        public int AnimationDurationMs = 250;

        public string FontFamily = "Microsoft YaHei UI";
        public double MainFontSize = 15.0;
        public double SubFontSize = 12.0;

        public string UITheme = "system"; // "system", "light", "dark"

        public bool AutoTheme = true;
        public string ColorMode = "system"; // "system", "white", "black", "custom"
        public string CustomColor = "31, 209, 224";
        public string MainTextColor = "#FFFFFF";
        public string SubTextColor = "#C8FFFFFF";
        public bool ShowBackgroundCard = false;
        public string BgCardColor = "#25000000";
        public bool ShadowEnabled = false;
        public string ShadowColor = "#000000";
        public double ShadowBlur = 3.0;

        public bool AutoHideWithTaskbar = true;
        public bool HideWhenFullscreen = true;
        public bool ShowTranslation = true;
        public bool RunAsAdmin = true;
        public bool SilentStart = true;

        public static AppConfig Load(string path) {
            AppConfig cfg = new AppConfig();
            try {
                if (File.Exists(path)) {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> dict = js.Deserialize<Dictionary<string, object>>(json);

                    if (dict != null) {
                        if (dict.ContainsKey("version") && dict["version"] != null) {
                            cfg.Version = dict["version"].ToString();
                        }

                        if (dict.ContainsKey("layout") && dict["layout"] is Dictionary<string, object>) {
                            var lay = (Dictionary<string, object>)dict["layout"];
                            if (lay.ContainsKey("position_mode")) cfg.PositionMode = lay["position_mode"].ToString();
                            if (lay.ContainsKey("x_offset")) cfg.XOffset = Convert.ToInt32(lay["x_offset"]);
                            if (lay.ContainsKey("y_offset")) cfg.YOffset = Convert.ToInt32(lay["y_offset"]);
                            if (lay.ContainsKey("width")) cfg.Width = Convert.ToInt32(lay["width"]);
                            if (lay.ContainsKey("height")) cfg.Height = Math.Max(48, Convert.ToInt32(lay["height"]));
                            if (lay.ContainsKey("show_cover")) cfg.ShowCover = Convert.ToBoolean(lay["show_cover"]);
                            if (lay.ContainsKey("rotate_cover")) cfg.RotateCover = Convert.ToBoolean(lay["rotate_cover"]);
                            if (lay.ContainsKey("cover_size")) cfg.CoverSize = Convert.ToInt32(lay["cover_size"]);
                            if (lay.ContainsKey("cover_radius")) cfg.CoverRadius = Convert.ToInt32(lay["cover_radius"]);
                        }

                        if (dict.ContainsKey("animation") && dict["animation"] is Dictionary<string, object>) {
                            var anim = (Dictionary<string, object>)dict["animation"];
                            if (anim.ContainsKey("type")) cfg.AnimationType = anim["type"].ToString();
                            if (anim.ContainsKey("duration_ms")) cfg.AnimationDurationMs = Convert.ToInt32(anim["duration_ms"]);
                        }

                        if (dict.ContainsKey("font") && dict["font"] is Dictionary<string, object>) {
                            var f = (Dictionary<string, object>)dict["font"];
                            if (f.ContainsKey("family")) cfg.FontFamily = f["family"].ToString();
                            if (f.ContainsKey("main_size")) cfg.MainFontSize = Convert.ToDouble(f["main_size"]);
                            if (f.ContainsKey("sub_size")) cfg.SubFontSize = Convert.ToDouble(f["sub_size"]);
                        }

                        if (dict.ContainsKey("color") && dict["color"] is Dictionary<string, object>) {
                            var c = (Dictionary<string, object>)dict["color"];
                            if (c.ContainsKey("auto_theme")) cfg.AutoTheme = Convert.ToBoolean(c["auto_theme"]);
                            if (c.ContainsKey("color_mode")) cfg.ColorMode = c["color_mode"].ToString();
                            if (c.ContainsKey("custom_color")) cfg.CustomColor = c["custom_color"].ToString();
                            if (c.ContainsKey("main_text")) cfg.MainTextColor = c["main_text"].ToString();
                            if (c.ContainsKey("sub_text")) cfg.SubTextColor = c["sub_text"].ToString();
                            if (c.ContainsKey("show_background_card")) cfg.ShowBackgroundCard = Convert.ToBoolean(c["show_background_card"]);
                            if (c.ContainsKey("bg_card_color")) cfg.BgCardColor = c["bg_card_color"].ToString();
                            if (c.ContainsKey("shadow_enabled")) cfg.ShadowEnabled = Convert.ToBoolean(c["shadow_enabled"]);
                            if (c.ContainsKey("shadow_color")) cfg.ShadowColor = c["shadow_color"].ToString();
                            if (c.ContainsKey("shadow_blur")) cfg.ShadowBlur = Convert.ToDouble(c["shadow_blur"]);

                            if (!c.ContainsKey("color_mode")) {
                                if (cfg.AutoTheme) {
                                    cfg.ColorMode = "system";
                                } else if (string.Equals(cfg.MainTextColor, "#FFFFFF", StringComparison.OrdinalIgnoreCase)) {
                                    cfg.ColorMode = "white";
                                } else if (string.Equals(cfg.MainTextColor, "#000000", StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(cfg.MainTextColor, "#1E1E1E", StringComparison.OrdinalIgnoreCase)) {
                                    cfg.ColorMode = "black";
                                } else {
                                    cfg.ColorMode = "custom";
                                    cfg.CustomColor = cfg.MainTextColor;
                                }
                            }
                            if (!c.ContainsKey("custom_color") && !string.IsNullOrEmpty(cfg.MainTextColor)) {
                                cfg.CustomColor = cfg.MainTextColor;
                            }
                        }

                        if (dict.ContainsKey("behavior") && dict["behavior"] is Dictionary<string, object>) {
                            var b = (Dictionary<string, object>)dict["behavior"];
                            if (b.ContainsKey("auto_hide_with_taskbar")) cfg.AutoHideWithTaskbar = Convert.ToBoolean(b["auto_hide_with_taskbar"]);
                            if (b.ContainsKey("hide_when_fullscreen")) cfg.HideWhenFullscreen = Convert.ToBoolean(b["hide_when_fullscreen"]);
                            if (b.ContainsKey("show_translation")) cfg.ShowTranslation = Convert.ToBoolean(b["show_translation"]);
                            if (b.ContainsKey("run_as_admin")) cfg.RunAsAdmin = Convert.ToBoolean(b["run_as_admin"]);
                            if (b.ContainsKey("silent_start")) cfg.SilentStart = Convert.ToBoolean(b["silent_start"]);
                        }

                        if (dict.ContainsKey("ui") && dict["ui"] is Dictionary<string, object>) {
                            var u = (Dictionary<string, object>)dict["ui"];
                            if (u.ContainsKey("theme")) cfg.UITheme = u["theme"].ToString();
                        }

                        // 如果已有的配置文件中未记录版本号，自动补充并写回
                        if (!dict.ContainsKey("version") || string.IsNullOrEmpty(cfg.Version)) {
                            cfg.Version = APP_VERSION;
                            cfg.Save(path);
                        }
                    }
                } else {
                    // 没有配置文件的情况下，以当前默认配置自动生成并持久化 config.json
                    cfg.Version = APP_VERSION;
                    cfg.Save(path);
                }
            } catch { }
            return cfg;
        }

        public void Save(string path) {
            try {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }

                var dict = new Dictionary<string, object>();
                dict["version"] = string.IsNullOrEmpty(Version) ? APP_VERSION : Version;
                dict["layout"] = new Dictionary<string, object> {
                    { "position_mode", PositionMode },
                    { "x_offset", XOffset },
                    { "y_offset", YOffset },
                    { "width", Width },
                    { "height", Height },
                    { "show_cover", ShowCover },
                    { "rotate_cover", RotateCover },
                    { "cover_size", CoverSize },
                    { "cover_radius", CoverRadius }
                };
                dict["animation"] = new Dictionary<string, object> {
                    { "type", AnimationType },
                    { "duration_ms", AnimationDurationMs }
                };
                dict["font"] = new Dictionary<string, object> {
                    { "family", FontFamily },
                    { "main_size", MainFontSize },
                    { "sub_size", SubFontSize }
                };
                dict["color"] = new Dictionary<string, object> {
                    { "color_mode", ColorMode },
                    { "custom_color", CustomColor },
                    { "auto_theme", ColorMode == "system" },
                    { "main_text", MainTextColor },
                    { "sub_text", SubTextColor },
                    { "show_background_card", ShowBackgroundCard },
                    { "bg_card_color", BgCardColor },
                    { "shadow_enabled", ShadowEnabled },
                    { "shadow_color", ShadowColor },
                    { "shadow_blur", ShadowBlur }
                };
                dict["behavior"] = new Dictionary<string, object> {
                    { "auto_hide_with_taskbar", AutoHideWithTaskbar },
                    { "hide_when_fullscreen", HideWhenFullscreen },
                    { "show_translation", ShowTranslation },
                    { "run_as_admin", RunAsAdmin },
                    { "silent_start", SilentStart }
                };
                dict["ui"] = new Dictionary<string, object> {
                    { "theme", UITheme }
                };

                JavaScriptSerializer js = new JavaScriptSerializer();
                string json = js.Serialize(dict);
                File.WriteAllText(path, json, Encoding.UTF8);
            } catch { }
        }
    }

    // ==========================================
    // 2. Win32 原生 API 封装
    // ==========================================
    public static class Win32 {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        public enum QUERY_USER_NOTIFICATION_STATE {
            QUNS_NOT_PRESENT = 1,
            QUNS_BUSY = 2,
            QUNS_RUNNING_D3D_FULL_SCREEN = 3,
            QUNS_PRESENTATION_MODE = 4,
            QUNS_ACCEPTS_NOTIFICATIONS = 5,
            QUNS_QUIET_TIME = 6,
            QUNS_APP = 7
        }

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE pquns);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fUnknown);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) {
            if (IntPtr.Size == 8)
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        public const uint MONITOR_DEFAULTTONULL = 0;
        public const uint MONITOR_DEFAULTTOPRIMARY = 1;
        public const uint GA_ROOT = 2;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;
        public const uint GW_OWNER = 4;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const int GWL_EXSTYLE = -20;
        public const int GWL_HWNDPARENT = -8; // 关键所有权：设置所属任务栏宿主，从根源杜绝点击任务栏闪烁
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOPMOST = 0x00000008;

        // SQLite API (系统内置 winsqlite3.dll)
        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open16", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_open16([MarshalAs(UnmanagedType.LPWStr)] string filename, out IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_open_v2(byte[] filenameUtf8, out IntPtr db, int flags, IntPtr zVfs);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_busy_timeout(IntPtr db, int ms);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_close(IntPtr db);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare16_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_prepare16_v2(IntPtr db, [MarshalAs(UnmanagedType.LPWStr)] string sql, int numBytes, out IntPtr stmt, IntPtr tail);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_step(IntPtr stmt);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)]
        public static extern long sqlite3_column_int64(IntPtr stmt, int col);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text16", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr sqlite3_column_text16(IntPtr stmt, int col);

        [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        public static extern int sqlite3_finalize(IntPtr stmt);

        public static bool IsSystemDarkTheme() {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    if (key != null) {
                        object val = key.GetValue("SystemUsesLightTheme");
                        if (val != null) {
                            return Convert.ToInt32(val) == 0;
                        }
                    }
                }
            } catch { }
            return true;
        }

        public static void SetAutoStart(bool enable) {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                    if (key != null) {
                        if (enable) {
                            string exePath = Process.GetCurrentProcess().MainModule.FileName;
                            key.SetValue("BodianTaskbarLyric", "\"" + exePath + "\"");
                        } else {
                            key.DeleteValue("BodianTaskbarLyric", false);
                        }
                    }
                }
            } catch { }
        }

        public static bool IsAutoStartEnabled() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false)) {
                    if (key != null) {
                        return key.GetValue("BodianTaskbarLyric") != null;
                    }
                }
            } catch { }
            return false;
        }

        public static bool IsAdministrator() {
            try {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            } catch {
                return false;
            }
        }

        public static void RestartElevated() {
            try {
                ProcessStartInfo psi = new ProcessStartInfo {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    WorkingDirectory = Program.AppDir,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                Application.Current.Shutdown();
            } catch { }
        }
    }

    // ==========================================
    // 2.5 颜色解析与格式化工具 (Color Helper)
    // ==========================================
    public static class ColorHelper {
        public static bool TryParseColor(string input, out Color color) {
            color = Colors.White;
            if (string.IsNullOrEmpty(input)) return false;
            string s = input.Trim();
            if (s.Length == 0) return false;

            if (s.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && s.EndsWith(")")) {
                s = s.Substring(4, s.Length - 5).Trim();
            } else if (s.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase) && s.EndsWith(")")) {
                s = s.Substring(5, s.Length - 6).Trim();
            }

            string[] parts = s.Split(new char[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3 || parts.Length == 4) {
                int r, g, b;
                if (int.TryParse(parts[0].Trim(), out r) && int.TryParse(parts[1].Trim(), out g) && int.TryParse(parts[2].Trim(), out b)) {
                    if (r >= 0 && r <= 255 && g >= 0 && g <= 255 && b >= 0 && b <= 255) {
                        int a = 255;
                        if (parts.Length == 4) {
                            int parsedA;
                            if (int.TryParse(parts[3].Trim(), out parsedA) && parsedA >= 0 && parsedA <= 255) {
                                a = parsedA;
                            }
                        }
                        color = Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);
                        return true;
                    }
                }
            }

            try {
                string hex = s;
                if (!hex.StartsWith("#")) {
                    if (hex.Length == 3 || hex.Length == 6 || hex.Length == 8) {
                        hex = "#" + hex;
                    }
                }
                object obj = ColorConverter.ConvertFromString(hex);
                if (obj is Color) {
                    color = (Color)obj;
                    return true;
                }
            } catch { }

            return false;
        }

        public static string ColorToRgbString(Color c) {
            return string.Format("{0}, {1}, {2}", c.R, c.G, c.B);
        }

        public static string ColorToHexString(Color c) {
            return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }

    // ==========================================
    // 3. 现代化胶囊开关控件 (Capsule Toggle Switch)
    // ==========================================
    public class CapsuleSwitch : System.Windows.Controls.UserControl {
        private Border _track;
        private Border _thumb;
        private TranslateTransform _thumbTransform;
        private bool _isChecked;
        private bool _isMuted = false;

        public event RoutedEventHandler Checked;
        public event RoutedEventHandler Unchecked;
        public event RoutedEventHandler Click;

        public bool IsChecked {
            get { return _isChecked; }
            set {
                if (_isChecked != value) {
                    _isChecked = value;
                    AnimateState(_isChecked, true);
                    if (!_isMuted) {
                        if (_isChecked && Checked != null) Checked(this, new RoutedEventArgs());
                        if (!_isChecked && Unchecked != null) Unchecked(this, new RoutedEventArgs());
                        if (Click != null) Click(this, new RoutedEventArgs());
                    }
                }
            }
        }

        private bool _isDark = false;

        public CapsuleSwitch() {
            Width = 46;
            Height = 26;
            Cursor = System.Windows.Input.Cursors.Hand;
            Background = Brushes.Transparent;
            Focusable = true;

            _track = new Border {
                Width = 46,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromRgb(229, 229, 234)), // #E5E5EA
                SnapsToDevicePixels = true
            };

            _thumbTransform = new TranslateTransform(3, 0);

            _thumb = new Border {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = _thumbTransform,
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.22,
                    Color = Colors.Black
                }
            };

            _track.Child = _thumb;
            Content = _track;

            PreviewMouseLeftButtonDown += (s, e) => {
                IsChecked = !IsChecked;
                e.Handled = true;
            };
        }

        public void ApplyTheme(bool isDark) {
            _isDark = isDark;
            _track.BorderThickness = new Thickness(_isDark ? 1.0 : 0.0);
            _track.BorderBrush = _isDark ? new SolidColorBrush(Color.FromRgb(72, 72, 76)) : Brushes.Transparent;
            AnimateState(_isChecked, false);
        }

        public void SetChecked(bool check, bool animate = false) {
            _isMuted = true;
            _isChecked = check;
            AnimateState(check, animate);
            _isMuted = false;
        }

        private void AnimateState(bool check, bool animate = true) {
            double targetX = check ? 23 : 3;
            Color offColor = _isDark ? Color.FromRgb(58, 58, 62) : Color.FromRgb(229, 229, 234);
            Color targetColor = check ? Color.FromRgb(0, 210, 106) : offColor;

            if (animate) {
                DoubleAnimation da = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(180)) {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                _thumbTransform.BeginAnimation(TranslateTransform.XProperty, da);

                ColorAnimation ca = new ColorAnimation(targetColor, TimeSpan.FromMilliseconds(180));
                _track.Background.BeginAnimation(SolidColorBrush.ColorProperty, ca);
            } else {
                _thumbTransform.BeginAnimation(TranslateTransform.XProperty, null);
                _track.Background.BeginAnimation(SolidColorBrush.ColorProperty, null);
                _thumbTransform.X = targetX;
                _track.Background = new SolidColorBrush(targetColor);
            }
        }
    }

    // ==========================================
    // 3.5 现代化 Fluent 风格圆角下拉选择框 (Modern Dropdown)
    // ==========================================
    public class ModernDropdownItem {
        public string Text { get; set; }
        public string Value { get; set; }
        public FontFamily Font { get; set; }
        public bool IsHeader { get; set; }

        public ModernDropdownItem(string text, string value, FontFamily font = null, bool isHeader = false) {
            Text = text;
            Value = value;
            Font = font;
            IsHeader = isHeader;
        }

        public override string ToString() {
            return Text;
        }
    }

    public class ModernDropdown : System.Windows.Controls.UserControl {
        private Border _headerBorder;
        private TextBlock _selectedText;
        private TextBlock _chevron;
        private Popup _popup;
        private Border _popupBorder;
        private StackPanel _itemsPanel;
        private ScrollViewer _scrollViewer;
        private List<ModernDropdownItem> _items = new List<ModernDropdownItem>();
        private int _selectedIndex = -1;
        private bool _isDark = false;

        public event EventHandler SelectionChanged;

        public List<ModernDropdownItem> Items { get { return _items; } }

        public int SelectedIndex {
            get { return _selectedIndex; }
            set {
                if (value >= 0 && value < _items.Count) {
                    _selectedIndex = value;
                    _selectedText.Text = _items[value].Text;
                    if (_items[value].Font != null) {
                        _selectedText.FontFamily = _items[value].Font;
                    } else {
                        _selectedText.FontFamily = new FontFamily("Segoe UI Variable Text, PingFang SC, Microsoft YaHei UI");
                    }
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                }
            }
        }

        public ModernDropdownItem SelectedItem {
            get {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count) return _items[_selectedIndex];
                return null;
            }
        }

        private int _lastClosedTick = 0;
        private bool _itemsBuilt = false;

        public ModernDropdown() {
            Height = 34;
            Cursor = System.Windows.Input.Cursors.Hand;
            Background = Brushes.Transparent;

            _headerBorder = new Border {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                SnapsToDevicePixels = true
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _selectedText = new TextBlock {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 6, 0),
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_selectedText, 0);
            g.Children.Add(_selectedText);

            _chevron = new TextBlock {
                Text = "⌵",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 12, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_chevron, 1);
            g.Children.Add(_chevron);

            _headerBorder.Child = g;

            _headerBorder.MouseEnter += (s, e) => {
                _headerBorder.Background = _isDark ? new SolidColorBrush(Color.FromRgb(40, 40, 44)) : new SolidColorBrush(Color.FromRgb(248, 249, 251));
                _headerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 210, 106));
            };
            _headerBorder.MouseLeave += (s, e) => {
                if (!_popup.IsOpen) ApplyTheme(_isDark);
            };

            _headerBorder.MouseLeftButtonUp += (s, e) => {
                if (Environment.TickCount - _lastClosedTick < 250) {
                    return;
                }
                OpenOrClosePopup();
                e.Handled = true;
            };

            _popup = new Popup {
                PlacementTarget = _headerBorder,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                VerticalOffset = 4
            };

            _popupBorder = new Border {
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                SnapsToDevicePixels = true,
                Padding = new Thickness(4),
                Effect = new DropShadowEffect {
                    BlurRadius = 16,
                    ShadowDepth = 4,
                    Opacity = 0.15,
                    Color = Colors.Black
                }
            };

            _scrollViewer = new ScrollViewer {
                MaxHeight = 240,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _itemsPanel = new StackPanel();
            _scrollViewer.Content = _itemsPanel;
            _popupBorder.Child = _scrollViewer;
            _popup.Child = _popupBorder;

            Grid rootGrid = new Grid();
            rootGrid.Children.Add(_headerBorder);
            rootGrid.Children.Add(_popup);
            Content = rootGrid;

            _popup.Opened += (s, e) => {
                Program.Log("ModernDropdown: _popup.Opened! ActualWidth=" + _popupBorder.ActualWidth + ", ActualHeight=" + _popupBorder.ActualHeight);
            };

            _popup.Closed += (s, e) => {
                _lastClosedTick = Environment.TickCount;
                Program.Log("ModernDropdown: _popup.Closed!");
                ApplyTheme(_isDark);
            };
        }

        public void OpenOrClosePopup() {
            Program.Log("ModernDropdown.OpenOrClosePopup: current IsOpen=" + _popup.IsOpen + ", itemsCount=" + _items.Count);
            try {
                if (_popup.IsOpen) {
                    _popup.IsOpen = false;
                } else {
                    RebuildItems();
                    _popupBorder.Width = Math.Max(ActualWidth, 240);
                    _popup.IsOpen = true;
                    Program.Log("ModernDropdown.OpenOrClosePopup: set IsOpen=true, Width=" + _popupBorder.Width);

                    if (_selectedIndex >= 0 && _selectedIndex < _itemsPanel.Children.Count) {
                        var selElem = _itemsPanel.Children[_selectedIndex] as FrameworkElement;
                        if (selElem != null) {
                            selElem.BringIntoView();
                        }
                    }
                }
            } catch (Exception ex) {
                Program.Log("ModernDropdown.OpenOrClosePopup EXCEPTION: " + ex.ToString());
            }
        }

        public void RebuildItems() {
            if (_itemsBuilt && _itemsPanel.Children.Count == _items.Count) {
                // Already built, simply refresh theme and selection highlight
                for (int i = 0; i < _items.Count; i++) {
                    var row = _itemsPanel.Children[i] as Border;
                    if (row != null && !_items[i].IsHeader) {
                        row.Background = (i == _selectedIndex) ?
                            (_isDark ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240))) :
                            Brushes.Transparent;
                        var tb = row.Child as TextBlock;
                        if (tb != null) {
                            tb.Foreground = (i == _selectedIndex) ?
                                new SolidColorBrush(Color.FromRgb(0, 168, 84)) :
                                (_isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30)));
                        }
                    }
                }
                return;
            }

            _itemsPanel.Children.Clear();
            for (int i = 0; i < _items.Count; i++) {
                int idx = i;
                var item = _items[i];
                if (item.IsHeader) {
                    Border hBorder = new Border {
                        Padding = new Thickness(10, 8, 10, 4),
                        Margin = new Thickness(0, 4, 0, 2)
                    };
                    TextBlock ht = new TextBlock {
                        Text = item.Text,
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    hBorder.Child = ht;
                    _itemsPanel.Children.Add(hBorder);
                    continue;
                }

                Border row = new Border {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 7, 12, 7),
                    Margin = new Thickness(0, 0, 0, 2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = (idx == _selectedIndex) ? 
                        (_isDark ? new SolidColorBrush(Color.FromRgb(22, 56, 38)) : new SolidColorBrush(Color.FromRgb(230, 249, 240))) :
                        Brushes.Transparent
                };

                TextBlock text = new TextBlock {
                    Text = item.Text,
                    FontSize = 12.5,
                    Foreground = (idx == _selectedIndex) ?
                        new SolidColorBrush(Color.FromRgb(0, 168, 84)) :
                        (_isDark ? Brushes.White : new SolidColorBrush(Color.FromRgb(28, 28, 30))),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                if (item.Font != null) {
                    text.FontFamily = item.Font;
                }

                row.Child = text;

                row.MouseEnter += (s, e) => {
                    if (idx != _selectedIndex) {
                        row.Background = _isDark ? new SolidColorBrush(Color.FromRgb(45, 45, 50)) : new SolidColorBrush(Color.FromRgb(242, 244, 247));
                    }
                };
                row.MouseLeave += (s, e) => {
                    if (idx != _selectedIndex) {
                        row.Background = Brushes.Transparent;
                    }
                };

                row.MouseLeftButtonDown += (s, e) => {
                    SelectedIndex = idx;
                    _popup.IsOpen = false;
                    e.Handled = true;
                };

                _itemsPanel.Children.Add(row);
            }
            _itemsBuilt = true;
        }

        public void ApplyTheme(bool isDark) {
            _isDark = isDark;
            if (isDark) {
                _headerBorder.Background = new SolidColorBrush(Color.FromRgb(36, 36, 38));
                _headerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 60));
                _selectedText.Foreground = Brushes.White;
                _chevron.Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 225));

                _popupBorder.Background = new SolidColorBrush(Color.FromRgb(36, 36, 38));
                _popupBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 60));
            } else {
                _headerBorder.Background = Brushes.White;
                _headerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233));
                _selectedText.Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30));
                _chevron.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));

                _popupBorder.Background = Brushes.White;
                _popupBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233));
            }

            if (_itemsBuilt) {
                RebuildItems();
            }
        }
    }

    // ==========================================
    // 4. 现代化圆角矩形按钮生成工厂 (Rounded Button Factory)
    // ==========================================
    public static class UIHelper {
        public static Button CreateRoundedButton(string text, Brush normalBg, Brush hoverBg, Brush fg, Brush borderBrush = null, double borderWidth = 0, double cornerRadius = 10, Thickness? padding = null, bool isBold = false) {
            Button btn = new Button {
                Content = text,
                Cursor = System.Windows.Input.Cursors.Hand,
                Padding = padding ?? new Thickness(18, 8, 18, 8),
                Foreground = fg,
                BorderThickness = new Thickness(0),
                FontWeight = isBold ? FontWeights.Bold : FontWeights.SemiBold,
                FontSize = 13,
                SnapsToDevicePixels = true
            };

            btn.Background = normalBg;
            if (borderBrush != null) btn.BorderBrush = borderBrush;
            btn.BorderThickness = new Thickness(borderWidth);

            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "btnBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            borderFactory.SetValue(Border.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.MarginProperty, new TemplateBindingExtension(Button.PaddingProperty));

            borderFactory.AppendChild(contentFactory);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = borderFactory;

            Trigger hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "btnBorder"));
            template.Triggers.Add(hoverTrigger);

            Trigger pressTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressTrigger.Setters.Add(new Setter(Border.OpacityProperty, 0.85, "btnBorder"));
            template.Triggers.Add(pressTrigger);

            btn.Template = template;
            return btn;
        }

        public static ControlTemplate BuildTitleBarButtonTemplate(bool isClose, bool isDark) {
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "btnBorder";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            template.VisualTree = borderFactory;

            Trigger mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            if (isClose) {
                Color hoverBg = isDark ? Color.FromRgb(196, 43, 28) : Color.FromRgb(254, 226, 226);
                Color hoverFg = isDark ? Colors.White : Color.FromRgb(239, 68, 68);
                mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "btnBorder"));
                mouseOverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(hoverFg)));
            } else {
                Color hoverBg = isDark ? Color.FromRgb(58, 58, 62) : Color.FromRgb(234, 234, 239);
                Color hoverFg = isDark ? Colors.White : Color.FromRgb(28, 28, 30);
                mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "btnBorder"));
                mouseOverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(hoverFg)));
            }
            template.Triggers.Add(mouseOverTrigger);

            Trigger pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            if (isClose) {
                Color pressBg = isDark ? Color.FromRgb(160, 30, 20) : Color.FromRgb(254, 202, 202);
                pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressBg), "btnBorder"));
            } else {
                Color pressBg = isDark ? Color.FromRgb(48, 48, 52) : Color.FromRgb(220, 220, 225);
                pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(pressBg), "btnBorder"));
            }
            template.Triggers.Add(pressedTrigger);
            return template;
        }

        public static void ApplyTitleBarButtonTheme(Button btn, bool isClose, bool isDark) {
            if (btn == null) return;
            btn.Foreground = isDark ? new SolidColorBrush(Color.FromRgb(210, 210, 215)) : new SolidColorBrush(Color.FromRgb(110, 110, 115));
            btn.Template = BuildTitleBarButtonTemplate(isClose, isDark);
        }

        public static Button CreateTitleBarButton(string symbol, Action onClick, bool isClose = false) {
            Button btn = new Button {
                Content = symbol,
                Width = 32,
                Height = 32,
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 115)),
                FontSize = isClose ? 12 : 14,
                FontWeight = FontWeights.Medium,
                BorderThickness = new Thickness(0),
                FocusVisualStyle = null
            };
            btn.Template = BuildTitleBarButtonTemplate(isClose, false);
            btn.Click += (s, e) => onClick();
            return btn;
        }
    }

    // ==========================================
    // 5. 歌词与歌曲模型 (Models)
    // ==========================================
    public class LyricLine {
        public double TimeSec;
        public string Original = "";
        public string Translation = "";
    }

    public class SongInfo {
        public long Id;
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public int Duration;
        public string PicUrl = "";
    }

    // ==========================================
    // 5.5 MPV 播放器内存直读同步器 (MpvMemoryReader)
    // ==========================================
    public class MpvMemoryReader {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint STILL_ACTIVE = 259;

        private int _targetPid = 0;
        private IntPtr _hProcess = IntPtr.Zero;
        private IntPtr _eventLoopBase = IntPtr.Zero;
        private long _mpctxAddr = 0;
        private long _lastAttemptTicks = 0;
        private int _readFailureCount = 0;
        private int _cachedMyHeadRva = 0xA1D8;

        public bool IsAvailable { get; private set; }
        public bool IsAccessDenied { get; private set; }
        public bool IsVerifiedPlaybackField { get; private set; }
        public double CurrentPts { get; private set; }

        public MpvMemoryReader() {
            CurrentPts = -1.0;
        }

        public void Reset() {
            if (_hProcess != IntPtr.Zero) {
                CloseHandle(_hProcess);
                _hProcess = IntPtr.Zero;
            }
            _targetPid = 0;
            _eventLoopBase = IntPtr.Zero;
            _mpctxAddr = 0;
            _readFailureCount = 0;
            IsAvailable = false;
            IsAccessDenied = false;
            IsVerifiedPlaybackField = false;
            CurrentPts = -1.0;
        }

        public void ResetPts() {
            CurrentPts = -1.0;
        }

        private long _lastPtsLogTick = 0;

        private long ReadInt64(long address) {
            if (_hProcess == IntPtr.Zero || address == 0) return 0;
            byte[] buf = new byte[8];
            IntPtr bytesRead;
            if (ReadProcessMemory(_hProcess, new IntPtr(address), buf, 8, out bytesRead) && bytesRead.ToInt32() == 8) {
                return BitConverter.ToInt64(buf, 0);
            }
            return 0;
        }

        public void Tick() {
            try {
                long now = Environment.TickCount;

                // 1. 如果已锁定有效的 mpctx，进行官方标准 time-pos 直读
                if (_hProcess != IntPtr.Zero && _mpctxAddr != 0) {
                    // 读取覆盖 0x2E0 ~ 0x3E0，包含 0x328 (time-pos)、0x318 (fallback)、0x3D0 (audio time)、0x2F0 (playback_pts)
                    byte[] block = new byte[0x100];
                    IntPtr bytesRead;
                    if (ReadProcessMemory(_hProcess, new IntPtr(_mpctxAddr + 0x2E0), block, block.Length, out bytesRead) && bytesRead.ToInt32() == block.Length) {
                        double t328 = BitConverter.ToDouble(block, 0x328 - 0x2E0); // +0x48
                        double t318 = BitConverter.ToDouble(block, 0x318 - 0x2E0); // +0x38
                        double t3d0 = BitConverter.ToDouble(block, 0x3D0 - 0x2E0); // +0xF0
                        double t2f0 = BitConverter.ToDouble(block, 0x2F0 - 0x2E0); // +0x10

                        double chosenPos = -1.0;
                        if (!double.IsNaN(t328) && !double.IsInfinity(t328) && t328 >= 0.0 && t328 < 86400.0) {
                            chosenPos = t328;
                        } else if (!double.IsNaN(t318) && !double.IsInfinity(t318) && t318 >= 0.0 && t318 < 86400.0) {
                            chosenPos = t318;
                        } else if (!double.IsNaN(t3d0) && !double.IsInfinity(t3d0) && t3d0 >= 0.0 && t3d0 < 86400.0) {
                            chosenPos = t3d0;
                        } else if (!double.IsNaN(t2f0) && !double.IsInfinity(t2f0) && t2f0 >= 0.0 && t2f0 < 86400.0) {
                            chosenPos = t2f0;
                        }

                        if (chosenPos >= 0.0) {
                            CurrentPts = chosenPos;
                            IsAvailable = true;
                            _readFailureCount = 0;

                            if (now - _lastPtsLogTick > 4000) {
                                _lastPtsLogTick = now;
                                Program.Log(string.Format("[MemReader] Tick: Pos={0:F2}s (328={1:F2}, 318={2:F2}, 2f0={3:F2}), mpctx=0x{4:X}", CurrentPts, t328, t318, t2f0, _mpctxAddr));
                            }
                            return;
                        }
                    }

                    _readFailureCount++;
                    // 连续 10 次读取失败（如播放实例重建或进程重启），立即重置重新连接
                    if (_readFailureCount > 10) {
                        Program.Log("[MemReader] Read failure threshold reached; resetting reader state.");
                        Reset();
                    }
                    return;
                }

                // 2. 尝试定位波点音乐进程与 mpctx
                if (_hProcess == IntPtr.Zero || _mpctxAddr == 0) {
                    if (now - _lastAttemptTicks < 1000) return;
                    _lastAttemptTicks = now;
                    TryAttach();
                    return;
                }
            } catch { }
        }

        private void TryAttach() {
            try {
                if (_hProcess != IntPtr.Zero) {
                    uint exitCode;
                    if (GetExitCodeProcess(_hProcess, out exitCode) && exitCode != STILL_ACTIVE) {
                        Reset();
                        return;
                    }
                }

                // 1. 获取波点音乐进程与基址
                if (_hProcess == IntPtr.Zero) {
                    Process[] procs = Process.GetProcessesByName("bodian_pc");
                    if (procs.Length == 0) procs = Process.GetProcessesByName("bodian");
                    if (procs.Length == 0) {
                        IsAccessDenied = false;
                        Reset();
                        return;
                    }

                    Process p = procs[0];
                    _targetPid = p.Id;

                    _eventLoopBase = IntPtr.Zero;
                    try {
                        foreach (ProcessModule m in p.Modules) {
                            if (m.ModuleName.IndexOf("media_kit_native_event_loop", StringComparison.OrdinalIgnoreCase) >= 0) {
                                _eventLoopBase = m.BaseAddress;
                                break;
                            }
                        }
                    } catch (System.ComponentModel.Win32Exception wEx) {
                        if (wEx.NativeErrorCode == 5) IsAccessDenied = true;
                        return;
                    } catch {
                        return;
                    }

                    if (_eventLoopBase == IntPtr.Zero) return;

                    _hProcess = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, _targetPid);
                    if (_hProcess == IntPtr.Zero) {
                        int err = Marshal.GetLastWin32Error();
                        if (err == 5) IsAccessDenied = true;
                        return;
                    }

                    IsAccessDenied = false;
                }

                // 官方核心指针链解析：
                // 1. media_kit_native_event_loop.dll 中在 0xA1D0 维护 std::unordered_map
                // 2. 0xA1D8 为 myHead 头指针（若波点版本升级导致偏移微调，会自动通过特征码动态重定位）
                // 3. firstNode + 0x10 为 Map Key，即 libmpv 核心句柄 struct mpv_handle*
                // 4. struct mpv_handle 中，前 4 字节为客户端标识 "main"，偏移 +0x48 即为 struct MPContext*
                // 5. struct MPContext 偏移 +0x328 处即为实时的播放时间戳 double time-pos（支持快进/快退/拖拽即时响应）
                if (_eventLoopBase != IntPtr.Zero && _hProcess != IntPtr.Zero) {
                    long myHead = ReadInt64(_eventLoopBase.ToInt64() + _cachedMyHeadRva);
                    long firstNode = myHead != 0 ? ReadInt64(myHead) : 0;
                    long mpvHandle = (firstNode != 0 && firstNode != myHead) ? ReadInt64(firstNode + 0x10) : 0;
                    bool isValid = false;

                    if (IsLikelyUserPointer(mpvHandle)) {
                        byte[] nameBuf = new byte[8];
                        IntPtr bytesRead;
                        if (ReadProcessMemory(_hProcess, new IntPtr(mpvHandle), nameBuf, 8, out bytesRead) && bytesRead.ToInt32() >= 4) {
                            if (nameBuf[0] == 'm' && nameBuf[1] == 'a' && nameBuf[2] == 'i' && nameBuf[3] == 'n') {
                                isValid = true;
                            }
                        }
                    }

                    if (!isValid) {
                        // 客户端版本更新自愈机制：通过特征码从 DLL 二进制动态重定位 singleton 与 myHead RVA
                        int dynamicRva = ResolveMyHeadRvaDynamically(_eventLoopBase);
                        if (dynamicRva != _cachedMyHeadRva) {
                            _cachedMyHeadRva = dynamicRva;
                            myHead = ReadInt64(_eventLoopBase.ToInt64() + _cachedMyHeadRva);
                            firstNode = myHead != 0 ? ReadInt64(myHead) : 0;
                            mpvHandle = (firstNode != 0 && firstNode != myHead) ? ReadInt64(firstNode + 0x10) : 0;
                            if (IsLikelyUserPointer(mpvHandle)) {
                                byte[] nameBuf = new byte[8];
                                IntPtr bytesRead;
                                if (ReadProcessMemory(_hProcess, new IntPtr(mpvHandle), nameBuf, 8, out bytesRead) && bytesRead.ToInt32() >= 4) {
                                    if (nameBuf[0] == 'm' && nameBuf[1] == 'a' && nameBuf[2] == 'i' && nameBuf[3] == 'n') {
                                        isValid = true;
                                    }
                                }
                            }
                        }
                    }

                    if (!isValid) return;

                    // 读取 MPContext*
                    long mpctx = ReadInt64(mpvHandle + 0x48);
                    if (!IsLikelyUserPointer(mpctx)) return;

                    _mpctxAddr = mpctx;
                    IsVerifiedPlaybackField = true;
                    IsAvailable = true;
                    Program.Log(string.Format("[MemReader] Attached successfully! mpvHandle=0x{0:X}, mpctx=0x{1:X}, time-pos=+0x328", mpvHandle, mpctx));
                }
            } catch (Exception ex) {
                Program.Log("[MemReader] TryAttach EX: " + ex.Message);
            }
        }

        private int ResolveMyHeadRvaDynamically(IntPtr modBase) {
            try {
                if (_hProcess == IntPtr.Zero || modBase == IntPtr.Zero) return 0xA1D8;
                byte[] mem = new byte[0x10000];
                IntPtr bytesRead;
                if (ReadProcessMemory(_hProcess, modBase, mem, mem.Length, out bytesRead)) {
                    int len = bytesRead.ToInt32();
                    for (int i = 0; i < len - 13; i++) {
                        if (mem[i] == 0x48 && mem[i + 1] == 0x8D && mem[i + 2] == 0x05 &&
                            mem[i + 7] == 0x48 && mem[i + 8] == 0x83 && mem[i + 9] == 0xC4 &&
                            mem[i + 10] == 0x20 && mem[i + 11] == 0x5B && mem[i + 12] == 0xC3) {
                            int disp = BitConverter.ToInt32(mem, i + 3);
                            int singletonRva = (i + 7) + disp;
                            int myHeadRva = singletonRva + 0x58;
                            Program.Log(string.Format("[MemReader] Dynamically discovered myHead RVA: 0x{0:X} (singleton=0x{1:X})", myHeadRva, singletonRva));
                            return myHeadRva;
                        }
                    }
                }
            } catch (Exception ex) {
                Program.Log("[MemReader] ResolveMyHeadRvaDynamically EX: " + ex.Message);
            }
            return 0xA1D8;
        }

        private static bool IsLikelyUserPointer(long value) {
            ulong u = unchecked((ulong)value);
            return u >= 0x10000UL && u < 0x0000800000000000UL;
        }
    }

    // ==========================================
    // 6. 波点音乐数据监听引擎 (Bodian Engine)
    // ==========================================
    public class BodianEngine {
        private string _dbPath;
        private string _logDir;
        private string _cacheDir;

        private long _lastOrd = -1;
        private string _lastSongTimeStr = "";
        public SongInfo CurrentSong { get; private set; }
        private ImageSource _currentCover;
        private List<LyricLine> _lyrics = new List<LyricLine>();
        private int _currentLyricIndex = -1;

        private Stopwatch _playStopwatch = new Stopwatch();
        public bool IsPlaying { get; private set; }
        private double _playTimeOffset = 0;
        private object _timeLock = new object();
        private long _lastProcessCheckMs = 0;
        private bool _playerProcessPresent = false;
        private double _lastMemPts = -1;
        private long _lastMemPtsChangeMs = 0;
        private volatile bool _memoryPtsStale = false;
        private volatile bool _waitingForTrackReset = false;
        private long _trackSwitchTick = 0;

        private MpvMemoryReader _memReader = new MpvMemoryReader();

        public bool IsMemorySyncActive {
            get { return _memReader != null && _memReader.IsAvailable && _memReader.CurrentPts >= 0 && !_memoryPtsStale && !_waitingForTrackReset; }
        }

        public bool IsMemoryAccessDenied {
            get { return _memReader != null && _memReader.IsAccessDenied; }
        }

        public event Action<SongInfo, ImageSource> OnSongChanged;
        public event Action<ImageSource> OnCoverChanged;
        public event Action<LyricLine, int> OnLyricChanged;
        public event Action<bool> OnPlayStateChanged;
        public event Action<bool> OnProcessStateChanged;

        // 默认波点黑胶唱片图标 (矢量生成，彻底避免封面缺失与白块)
        public static readonly ImageSource DefaultCover = CreateDefaultBodianIcon(96);

        public double CurrentPlaybackSeconds {
            get {
                lock (_timeLock) {
                    if (IsMemorySyncActive) {
                        return Math.Max(0, _memReader.CurrentPts);
                    }
                    return Math.Max(0, _playTimeOffset + (IsPlaying ? _playStopwatch.Elapsed.TotalSeconds : 0));
                }
            }
        }

        public bool IsBodianRunning {
            get { return _playerProcessPresent; }
        }

        public BodianEngine() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbPath = Path.Combine(localAppData, @"cn.wenyu.bodian\bodian_pc\database\songDB.db");
            _logDir = Path.Combine(localAppData, @"cn.wenyu.bodian\bodian_pc\bdlog");
            _cacheDir = Path.Combine(localAppData, @"BodianTaskbarLyric\cache");
            if (!Directory.Exists(_cacheDir)) Directory.CreateDirectory(_cacheDir);
            _playerProcessPresent = IsBodianProcessRunning();
        }

        public bool IsBodianProcessRunning() {
            try {
                return Process.GetProcessesByName("bodian_pc").Length > 0 || Process.GetProcessesByName("bodian").Length > 0;
            } catch {
                return false;
            }
        }

        private void SetPlayingState(bool playing) {
            bool changed;
            lock (_timeLock) {
                changed = IsPlaying != playing;
                if (!changed) return;

                if (playing) {
                    if (CurrentSong != null && _playTimeOffset <= 0.5) {
                        double elapsed = GetElapsedFromSongStart(_lastSongTimeStr, CurrentSong.Duration);
                        if (elapsed > 0.5) _playTimeOffset = elapsed;
                    }
                    _playStopwatch.Restart();
                } else {
                    if (_playStopwatch.IsRunning) {
                        _playTimeOffset += _playStopwatch.Elapsed.TotalSeconds;
                    }
                    _playStopwatch.Reset();
                }
                IsPlaying = playing;
            }

            if (OnPlayStateChanged != null) OnPlayStateChanged(playing);
        }

        private double GetElapsedFromSongStart(string timeStr, int duration) {
            try {
                DateTime songStart;
                if (!string.IsNullOrEmpty(timeStr) && DateTime.TryParse(timeStr, out songStart)) {
                    double elapsed = (DateTime.Now - songStart).TotalSeconds;
                    if (elapsed >= 0 && (duration <= 0 || elapsed < duration + 15)) return elapsed;
                }
            } catch { }
            return 0;
        }

        private void HandlePositionLogLine(string line) {
            try {
                if (_memReader.IsVerifiedPlaybackField) return;

                Match m = Regex.Match(line, @"play old song info id:(\d+).*?position:([0-9]+).*?new song info id:(\d+)", RegexOptions.IgnoreCase);
                if (!m.Success || CurrentSong == null) return;

                long oldSongId;
                long newSongId;
                double position;
                if (!long.TryParse(m.Groups[1].Value, out oldSongId) ||
                    !double.TryParse(m.Groups[2].Value, out position) ||
                    !long.TryParse(m.Groups[3].Value, out newSongId) ||
                    oldSongId != CurrentSong.Id || newSongId != CurrentSong.Id) {
                    return;
                }

                lock (_timeLock) {
                    _playTimeOffset = Math.Max(0, position);
                    _memoryPtsStale = !_memReader.IsVerifiedPlaybackField;
                    _currentLyricIndex = -1;
                    if (IsPlaying) _playStopwatch.Restart();
                    else _playStopwatch.Reset();
                }
                Program.Log(string.Format("[LogSync] Position={0:F2}s for song {1}; lyric index reset.", position, CurrentSong.Id));
                UpdatePlaybackTime();
            } catch { }
        }

        private void HandleSongLogLine(string line) {
            try {
                if (string.IsNullOrEmpty(line)) return;

                // 场景 1：歌曲完整参数信息 (包含完整元数据)
                int idxFull = line.IndexOf("歌曲的完整参数信息:");
                if (idxFull >= 0) {
                    string payload = line.Substring(idxFull + "歌曲的完整参数信息:".Length).Trim();
                    Match mId = Regex.Match(payload, @"\bid:\s*(\d+)");
                    if (!mId.Success) return;

                    long id = long.Parse(mId.Groups[1].Value);
                    if (CurrentSong != null && CurrentSong.Id == id && _lyrics != null && _lyrics.Count > 0) {
                        return;
                    }

                    SongInfo song = new SongInfo();
                    song.Id = id;

                    Match mName = Regex.Match(payload, @"\bname:\s*(.*?)(?=,\s*[a-zA-Z0-9_]+:|\}$)");
                    if (mName.Success) song.Title = mName.Groups[1].Value.Trim();

                    Match mArtist = Regex.Match(payload, @"\bartist:\s*(.*?)(?=,\s*[a-zA-Z0-9_]+:|\}$)");
                    if (mArtist.Success) song.Artist = mArtist.Groups[1].Value.Trim();

                    Match mAlbum = Regex.Match(payload, @"\balbum:\s*(.*?)(?=,\s*[a-zA-Z0-9_]+:|\}$)");
                    if (mAlbum.Success) song.Album = mAlbum.Groups[1].Value.Trim();

                    Match mDur = Regex.Match(payload, @"\bduration:\s*(\d+)");
                    if (mDur.Success) {
                        int dur;
                        if (int.TryParse(mDur.Groups[1].Value, out dur)) song.Duration = dur;
                    }

                    Match mPic120 = Regex.Match(payload, @"\balbumPic120:\s*([^,\s\}]+)");
                    if (mPic120.Success) {
                        song.PicUrl = mPic120.Groups[1].Value.Trim();
                    } else {
                        Match mPic = Regex.Match(payload, @"\balbumPic:\s*([^,\s\}]+)");
                        if (mPic.Success) song.PicUrl = mPic.Groups[1].Value.Trim();
                    }

                    Program.Log(string.Format("[LogMonitor] Song detected from log (full info): id={0}, name='{1}', artist='{2}'",
                        song.Id, song.Title, song.Artist));
                    SwitchToSong(song, null, false);
                }
            } catch (Exception ex) {
                Program.Log("[LogMonitor] HandleSongLogLine EX: " + ex.ToString());
            }
        }

        public static ImageSource CreateDefaultBodianIcon(int size) {
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen()) {
                double center = size / 2.0;

                // 1. 深黑黑胶唱片底盘
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 28, 30)), null, new Point(center, center), center, center);

                // 2. 唱片同心纹理线 (半透明白环)
                Pen groovePen1 = new Pen(new SolidColorBrush(Color.FromArgb(35, 255, 255, 255)), 1.2);
                dc.DrawEllipse(null, groovePen1, new Point(center, center), size * 0.38, size * 0.38);
                Pen groovePen2 = new Pen(new SolidColorBrush(Color.FromArgb(25, 255, 255, 255)), 1.2);
                dc.DrawEllipse(null, groovePen2, new Point(center, center), size * 0.28, size * 0.28);

                // 3. 波点经典活力绿中心圆标
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0, 210, 106)), null, new Point(center, center), size * 0.17, size * 0.17);

                // 4. 黑胶中心轴孔
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 28, 30)), null, new Point(center, center), size * 0.05, size * 0.05);
            }

            RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        // 同步初始化：启动前立即拉取一次数据库记录
        public void Init() {
            try {
                CheckDatabase();
            } catch { }
        }

        // 启动后台持续监听线程
        public void Start() {
            Thread logWorker = new Thread(LogMonitorLoop) { IsBackground = true };
            logWorker.Start();

            Thread playWorker = new Thread(PlaybackLoop) { IsBackground = true };
            playWorker.Start();
        }

        public ImageSource GetCoverImage(long songId, string url) {
            if (_currentCover != null) return _currentCover;
            ImageSource img = LoadCoverImage(songId, url);
            _currentCover = img != null ? img : DefaultCover;
            return _currentCover;
        }

        private void PlaybackLoop() {
            while (true) {
                try {
                    long now = Environment.TickCount;
                    if (_lastProcessCheckMs == 0 || now - _lastProcessCheckMs >= 300) {
                        _lastProcessCheckMs = now;
                        bool running = IsBodianProcessRunning();
                        if (running != _playerProcessPresent) {
                            _playerProcessPresent = running;
                            if (!_playerProcessPresent) {
                                if (_memReader.IsAvailable) _memReader.Reset();
                                _lastMemPts = -1;
                                _lastMemPtsChangeMs = 0;
                                _memoryPtsStale = false;
                                SetPlayingState(false);
                            }
                            if (OnProcessStateChanged != null) OnProcessStateChanged(_playerProcessPresent);
                        }
                    }

                    _memReader.Tick();
                    if (_memReader.IsAvailable && _memReader.CurrentPts >= 0) {
                        double memPts = _memReader.CurrentPts;

                        // 核心防跳机制：切歌瞬时底层 MPV 仍需 200~350ms 释放并重置旧音频
                        // 在 MPV 进度尚未归零前（仍残留上一首歌的 134s、75s 等秒数），坚决屏蔽该旧进度，杜绝切歌时歌词跳跃
                        if (_waitingForTrackReset) {
                            if (memPts <= 1.5) {
                                _waitingForTrackReset = false;
                                Program.Log(string.Format("[PlaybackLoop] MPV reset confirmed: memPts={0:F2}s", memPts));
                                _lastMemPts = memPts;
                                _lastMemPtsChangeMs = now;
                                _memoryPtsStale = false;
                                lock (_timeLock) {
                                    _playTimeOffset = memPts;
                                    if (IsPlaying) _playStopwatch.Restart();
                                    else _playStopwatch.Reset();
                                }
                            } else if (now - _trackSwitchTick > 2500) {
                                _waitingForTrackReset = false;
                                Program.Log(string.Format("[PlaybackLoop] Track reset wait timeout: memPts={0:F2}s", memPts));
                            } else {
                                UpdatePlaybackTime();
                                Thread.Sleep(25);
                                continue;
                            }
                        }

                        bool hadPreviousMemPts = _lastMemPts >= 0;
                        double previousMemPts = _lastMemPts;
                        double memDelta = hadPreviousMemPts ? memPts - previousMemPts : 0;
                        bool memPtsChanged = !hadPreviousMemPts || Math.Abs(memDelta) > 0.001;
                        if (memPtsChanged) {
                            _lastMemPts = memPts;
                            _lastMemPtsChangeMs = now;
                            _memoryPtsStale = false;
                        } else if (!_memReader.IsVerifiedPlaybackField && IsPlaying && _lastMemPtsChangeMs > 0 && now - _lastMemPtsChangeMs >= 1500) {
                            if (!_memoryPtsStale) {
                                _memoryPtsStale = true;
                                Program.Log("[PlaybackLoop] Memory PTS is stale; falling back to stopwatch sync.");
                            }
                        }

                        double currentEstimate;
                        lock (_timeLock) {
                            currentEstimate = Math.Max(0, _playTimeOffset + (IsPlaying ? _playStopwatch.Elapsed.TotalSeconds : 0));
                        }
                        if (!_memReader.IsVerifiedPlaybackField && !hadPreviousMemPts && memPts <= 0.5 && currentEstimate > 1.0) {
                            _memoryPtsStale = true;
                            Program.Log(string.Format("[PlaybackLoop] Ignoring implausible initial memory PTS={0:F2}; keeping local estimate={1:F2}.", memPts, currentEstimate));
                        }

                        if (!_memoryPtsStale && memPtsChanged) {
                            lock (_timeLock) {
                                double curEst = Math.Max(0, _playTimeOffset + (IsPlaying ? _playStopwatch.Elapsed.TotalSeconds : 0));
                                bool initialValueIsPlausible = hadPreviousMemPts || memPts > 0.5 || curEst <= 1.0;
                                bool seekDetected = hadPreviousMemPts && (memDelta < -0.25 || memDelta > 1.2);
                                bool initialPosition = !hadPreviousMemPts && initialValueIsPlausible && Math.Abs(memPts - curEst) > 1.0;
                                if (seekDetected || initialPosition) {
                                    Program.Log(string.Format("[PlaybackLoop] SEEK DETECTED: curEst={0:F2} -> memPts={1:F2} (delta={2:F2})", curEst, memPts, memDelta));
                                    _playTimeOffset = memPts;
                                    if (IsPlaying) _playStopwatch.Restart();
                                    else _playStopwatch.Reset();
                                    _currentLyricIndex = -1; // 进度拖动或跳变，强制刷新定位歌词
                                }
                            }
                        }
                    }

                    UpdatePlaybackTime();
                } catch (Exception ex) {
                    Program.Log("[PlaybackLoop] EX: " + ex.ToString());
                }
                Thread.Sleep(25);
            }
        }

        private void CheckDatabase() {
            if (!File.Exists(_dbPath)) return;

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;
            try {
                byte[] pathBytes = Encoding.UTF8.GetBytes(_dbPath + "\0");
                int rc = Win32.sqlite3_open_v2(pathBytes, out db, 0x00000001, IntPtr.Zero); // 0x00000001 = SQLITE_OPEN_READONLY
                if (rc != 0) return;

                Win32.sqlite3_busy_timeout(db, 1000);

                string sql = "SELECT ord, id, json, time FROM hist_song ORDER BY ord DESC LIMIT 1;";
                rc = Win32.sqlite3_prepare16_v2(db, sql, -1, out stmt, IntPtr.Zero);
                if (rc == 0 && Win32.sqlite3_step(stmt) == 100) {
                    long ord = Win32.sqlite3_column_int64(stmt, 0);
                    long id = Win32.sqlite3_column_int64(stmt, 1);
                    IntPtr textPtr = Win32.sqlite3_column_text16(stmt, 2);
                    string json = textPtr != IntPtr.Zero ? Marshal.PtrToStringUni(textPtr) : "";
                    IntPtr timePtr = Win32.sqlite3_column_text16(stmt, 3);
                    string timeStr = timePtr != IntPtr.Zero ? Marshal.PtrToStringUni(timePtr) : "";
                    _lastSongTimeStr = timeStr;

                    if (ord != _lastOrd) {
                        bool isFirstRun = (_lastOrd == -1);
                        _lastOrd = ord;
                        HandleNewSong(id, json, timeStr, isFirstRun);
                    }
                }
            } catch (Exception ex) {
                Program.Log("[CheckDatabase] EX: " + ex.ToString());
            } finally {
                if (stmt != IntPtr.Zero) Win32.sqlite3_finalize(stmt);
                if (db != IntPtr.Zero) Win32.sqlite3_close(db);
            }
        }

        private void HandleNewSong(long id, string json, string timeStr, bool isFirstRun) {
            try {
                JavaScriptSerializer js = new JavaScriptSerializer();
                Dictionary<string, object> dict = js.Deserialize<Dictionary<string, object>>(json);

                SongInfo song = new SongInfo();
                song.Id = id;
                if (dict != null) {
                    if (dict.ContainsKey("name") && dict["name"] != null) song.Title = dict["name"].ToString();
                    if (dict.ContainsKey("artist") && dict["artist"] != null) song.Artist = dict["artist"].ToString();
                    if (dict.ContainsKey("album") && dict["album"] != null) song.Album = dict["album"].ToString();
                    if (dict.ContainsKey("duration") && dict["duration"] != null) song.Duration = Convert.ToInt32(dict["duration"]);
                    if (dict.ContainsKey("albumPic120") && dict["albumPic120"] != null) song.PicUrl = dict["albumPic120"].ToString();
                    else if (dict.ContainsKey("albumPic") && dict["albumPic"] != null) song.PicUrl = dict["albumPic"].ToString();
                }

                SwitchToSong(song, timeStr, isFirstRun);
            } catch (Exception ex) {
                Program.Log("[Engine] HandleNewSong EX: " + ex.ToString());
            }
        }

        private void SwitchToSong(SongInfo song, string timeStr, bool isFirstRun) {
            if (song == null) return;
            try {
                if (!isFirstRun && CurrentSong != null && CurrentSong.Id == song.Id) {
                    if (!string.IsNullOrEmpty(song.PicUrl) && (_currentCover == null || _currentCover == DefaultCover)) {
                        ThreadPool.QueueUserWorkItem(state => {
                            try {
                                ImageSource loaded = LoadCoverImage(song.Id, song.PicUrl);
                                if (loaded != null && CurrentSong != null && CurrentSong.Id == song.Id) {
                                    _currentCover = loaded;
                                    if (OnCoverChanged != null) OnCoverChanged(_currentCover);
                                }
                            } catch { }
                        });
                    }
                    return;
                }

                CurrentSong = song;
                _currentCover = null;
                _lastMemPts = -1;
                _lastMemPtsChangeMs = 0;
                _memoryPtsStale = false;
                if (!isFirstRun) {
                    _waitingForTrackReset = true;
                    _trackSwitchTick = Environment.TickCount;
                } else {
                    _waitingForTrackReset = false;
                }
                if (_memReader != null) _memReader.ResetPts();
                lock (_timeLock) {
                    _lyrics = new List<LyricLine>();
                    _currentLyricIndex = -1;
                }
                Program.Log(string.Format("[Engine] SwitchToSong: ord={0}, id={1}, title='{2}', artist='{3}', dur={4}, isFirstRun={5}",
                    _lastOrd, song.Id, song.Title, song.Artist, song.Duration, isFirstRun));

                bool wasPlaying = IsPlaying;
                if (isFirstRun) {
                    _playTimeOffset = wasPlaying ? GetElapsedFromSongStart(timeStr, song.Duration) : 0;
                    if (wasPlaying) _playStopwatch.Restart();
                    else _playStopwatch.Reset();
                } else {
                    _playTimeOffset = 0;
                    if (wasPlaying) _playStopwatch.Restart();
                    else _playStopwatch.Reset();
                }

                if (OnPlayStateChanged != null) OnPlayStateChanged(IsPlaying);

                if (OnSongChanged != null) {
                    OnSongChanged(song, DefaultCover);
                }

                ThreadPool.QueueUserWorkItem(state => {
                    try {
                        ImageSource loaded = LoadCoverImage(song.Id, song.PicUrl);
                        _currentCover = loaded != null ? loaded : DefaultCover;
                        if (CurrentSong != null && CurrentSong.Id == song.Id) {
                            if (OnCoverChanged != null) OnCoverChanged(_currentCover);
                        }
                    } catch { }
                });

                ThreadPool.QueueUserWorkItem(state => {
                    try {
                        List<LyricLine> list = FetchLyrics(song.Id, song.Title);
                        if (CurrentSong != null && CurrentSong.Id == song.Id) {
                            lock (_timeLock) {
                                _lyrics = list;
                                _currentLyricIndex = -1;
                            }
                            Program.Log(string.Format("[Engine] FetchLyrics complete for '{0}' (count={1})", song.Title, list != null ? list.Count : 0));
                            UpdatePlaybackTime();
                        }
                    } catch { }
                });
            } catch (Exception ex) {
                Program.Log("[Engine] SwitchToSong EX: " + ex.ToString());
            }
        }

        private void UpdatePlaybackTime() {
            if (CurrentSong == null || _lyrics == null || _lyrics.Count == 0) return;

            double curSec = CurrentPlaybackSeconds;

            int activeIndex = -1;
            for (int i = 0; i < _lyrics.Count; i++) {
                if (curSec >= _lyrics[i].TimeSec) {
                    activeIndex = i;
                } else {
                    break;
                }
            }

            if (activeIndex != _currentLyricIndex) {
                _currentLyricIndex = activeIndex;
                if (activeIndex >= 0) {
                    if (OnLyricChanged != null) {
                        LyricLine line = _lyrics[activeIndex];
                        Program.Log(string.Format("[LyricChanged] Sec={0:F2}, idx={1}: {2}", curSec, activeIndex, line.Original));
                        OnLyricChanged(line, activeIndex);
                    }
                }
            }
        }

        private void LogMonitorLoop() {
            long lastSize = 0;
            string lastLogFile = "";

            while (true) {
                try {
                    if (Directory.Exists(_logDir)) {
                        string[] files = Directory.GetFiles(_logDir, "log_*.log");
                        if (files.Length > 0) {
                            Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                            string curFile = files[0];

                            if (curFile != lastLogFile) {
                                lastLogFile = curFile;
                                FileInfo fi = new FileInfo(curFile);
                                long curSize = fi.Length;

                                long seekStart = Math.Max(0, curSize - 65536);
                                using (FileStream fs = new FileStream(curFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                    fs.Seek(seekStart, SeekOrigin.Begin);
                                    using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) {
                                        string line;
                                        bool? lastEvent = null;
                                        string lastSongLine = null;
                                        while ((line = sr.ReadLine()) != null) {
                                            if (line.Contains("歌曲的完整参数信息:")) {
                                                lastSongLine = line;
                                            }
                                            if (line.Contains("mpv playing event=true")) lastEvent = true;
                                            else if (line.Contains("mpv playing event=false")) lastEvent = false;
                                        }
                                        if (lastSongLine != null) {
                                            HandleSongLogLine(lastSongLine);
                                        }
                                        if (lastEvent.HasValue) {
                                            bool playState = lastEvent.Value && IsBodianProcessRunning();
                                            SetPlayingState(playState);
                                        }
                                    }
                                }
                                lastSize = curSize;
                            } else {
                                FileInfo fi = new FileInfo(curFile);
                                long curSize = fi.Length;
                                if (curSize < lastSize) {
                                    lastSize = 0;
                                }
                                if (curSize > lastSize) {
                                    using (FileStream fs = new FileStream(curFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                                        fs.Seek(lastSize, SeekOrigin.Begin);
                                        using (StreamReader sr = new StreamReader(fs, Encoding.UTF8)) {
                                            string line;
                                            while ((line = sr.ReadLine()) != null) {
                                                HandleSongLogLine(line);
                                                HandlePositionLogLine(line);
                                                if (line.Contains("mpv playing event=true") && IsBodianProcessRunning()) {
                                                    SetPlayingState(true);
                                                } else if (line.Contains("mpv playing event=false")) {
                                                    SetPlayingState(false);
                                                }
                                            }
                                        }
                                    }
                                    lastSize = curSize;
                                }
                            }
                        }
                    }
                } catch { }
                Thread.Sleep(150);
            }
        }

        private ImageSource LoadCoverImage(long songId, string url) {
            try {
                string cacheFile = Path.Combine(_cacheDir, string.Format("{0}.jpg", songId));
                if (!File.Exists(cacheFile) && !string.IsNullOrEmpty(url)) {
                    using (WebClient client = new WebClient()) {
                        client.DownloadFile(url, cacheFile);
                    }
                }

                if (File.Exists(cacheFile)) {
                    byte[] bytes = File.ReadAllBytes(cacheFile);
                    BitmapImage bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = new MemoryStream(bytes);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 96;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            } catch { }
            return null;
        }

        private static bool IsSongTitleLine(string text, double sec, string songTitle) {
            if (string.IsNullOrEmpty(text)) return true;
            if (sec > 5.0) return false;

            string t = text.Trim();
            if (t.Length == 0) return true;

            // 1. 如果匹配当前歌名（完全匹配、去除括号后缀如 (Live) 的歌名、或者带有连字符的 "歌名 - 歌手"）
            if (!string.IsNullOrEmpty(songTitle)) {
                string cleanTitle = songTitle.Trim();
                if (string.Equals(t, cleanTitle, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                // 去除括号后缀（如 " (Live)", " [伴奏]"）提取纯标题名
                string baseTitle = Regex.Replace(cleanTitle, @"\s*[\(\[（【].*?[\)\]）】]", "").Trim();
                if (!string.IsNullOrEmpty(baseTitle) && string.Equals(t, baseTitle, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }

                string checkTitle = !string.IsNullOrEmpty(baseTitle) && baseTitle.Length >= 2 ? baseTitle : cleanTitle;
                if (checkTitle.Length >= 2 && t.IndexOf(checkTitle, StringComparison.OrdinalIgnoreCase) >= 0) {
                    if (t.Contains(" - ") || t.Contains(" — ") || t.Contains(" / ") || t.Contains(" · ") ||
                        t.StartsWith("歌名") || t.StartsWith("歌曲") || t.StartsWith("曲名") ||
                        t.StartsWith("Title", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }

            // 2. 0~2.5 秒内带有连字符或歌名标识的分隔行（如 "Title - Artist", "歌名：xxx"）
            if (sec <= 2.5) {
                if (t.Contains(" - ") || t.Contains(" — ") ||
                    t.StartsWith("歌名") || t.StartsWith("歌曲") || t.StartsWith("曲名") ||
                    t.StartsWith("Title", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        private List<LyricLine> FetchLyrics(long songId, string songTitle) {
            List<LyricLine> list = new List<LyricLine>();
            try {
                string rawQ = string.Format("type=lyric&req=2&lrcx=1&rid={0}&songname=&artist=&corp=kuwo&fromchannel=bodian", songId);
                string q = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawQ));
                string url = string.Format("http://mlyric.kuwo.cn/mobi.s?f=bodian&q={0}", q);

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "okhttp/3.10.0";
                req.Timeout = 4000;

                string rawLrc = "";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) {
                    string json = sr.ReadToEnd();
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
                    if (d.ContainsKey("data") && d["data"] is Dictionary<string, object>) {
                        var data = (Dictionary<string, object>)d["data"];
                        if (data.ContainsKey("content") && data["content"] != null) {
                            byte[] b64Bytes = Convert.FromBase64String(data["content"].ToString());
                            rawLrc = Encoding.UTF8.GetString(b64Bytes);
                        }
                    }
                }

                if (string.IsNullOrEmpty(rawLrc)) return list;

                Regex reg = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)");
                LyricLine lastOriginalLine = null;

                string[] lines = rawLrc.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines) {
                    Match m = reg.Match(line.Trim());
                    if (m.Success) {
                        int min = int.Parse(m.Groups[1].Value);
                        int sec = int.Parse(m.Groups[2].Value);
                        int ms = int.Parse(m.Groups[3].Value);
                        if (m.Groups[3].Value.Length == 2) ms *= 10;
                        double totalSec = min * 60 + sec + ms / 1000.0;

                        string content = m.Groups[4].Value;

                        if (content.Contains("<0,0>")) {
                            // 酷狗LRCX格式中，<0,0>为上一句主歌词的翻译文本（标记在上一句演唱完毕/下一句起始时间点）
                            string text = Regex.Replace(content, @"<[^>]+>", "").Trim();
                            if (!string.IsNullOrEmpty(text) && lastOriginalLine != null) {
                                lastOriginalLine.Translation = text;
                            }
                        } else {
                            string text = Regex.Replace(content, @"<[^>]+>", "").Trim();
                            bool isHeaderTitle = (list.Count == 0 || totalSec <= 1.0) && IsSongTitleLine(text, totalSec, songTitle);
                            if (!string.IsNullOrEmpty(text) && !text.StartsWith("[") && !isHeaderTitle) {
                                LyricLine cur = new LyricLine {
                                    TimeSec = totalSec,
                                    Original = text,
                                    Translation = ""
                                };
                                list.Add(cur);
                                lastOriginalLine = cur;
                            }
                        }
                    }
                }

                list.Sort((a, b) => a.TimeSec.CompareTo(b.TimeSec));

            } catch { }
            return list;
        }
    }

    // ==========================================
    // 7. 任务栏歌词悬浮条 (Taskbar Overlay Window)
    // ==========================================
    public class TaskbarOverlayWindow : Window {
        private AppConfig _config;
        private string _configPath;
        private BodianEngine _engine;
        private DispatcherTimer _taskbarFollowTimer;

        private Border _rootCard;
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

        // 缓存上一次坐标与尺寸，防止无意义重绘与点击闪烁
        private int _lastX = -9999;
        private int _lastY = -9999;
        private int _lastW = -9999;
        private int _lastH = -9999;

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

            // 初始化当前歌曲信息
            if (_engine.CurrentSong != null) {
                _mainText.Text = _engine.CurrentSong.Title;
                _subText.Text = _engine.CurrentSong.Artist;
                _subText.Visibility = Visibility.Visible;
                _mainText.Margin = new Thickness(0, 0, 0, 0);
                ImageSource initCover = _engine.GetCoverImage(_engine.CurrentSong.Id, _engine.CurrentSong.PicUrl);
                _coverBrush.ImageSource = initCover != null ? initCover : BodianEngine.DefaultCover;
            } else {
                _coverBrush.ImageSource = BodianEngine.DefaultCover;
            }

            _engine.OnSongChanged += Engine_OnSongChanged;
            _engine.OnCoverChanged += Engine_OnCoverChanged;
            _engine.OnLyricChanged += Engine_OnLyricChanged;
            _engine.OnPlayStateChanged += playing => {
                Dispatcher.Invoke(() => UpdateRotationState());
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
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            SourceInitialized += (s, e) => {
                _hwnd = new WindowInteropHelper(this).Handle;

                // 核心防闪烁机制：将悬浮窗所有权关联到任务栏 Shell_TrayWnd
                // Windows 原生机制保证 Owned Window 永远排列在 Owner 之前，彻底杜绝切换/点击任务栏时的画面闪烁
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

            MouseLeftButtonDown += (s, e) => {
                if (_onOpenSettings != null) _onOpenSettings();
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
            miExit.Click += (s, e) => Application.Current.Shutdown();

            menu.Items.Add(miSettings);
            menu.Items.Add(miMode);
            menu.Items.Add(new System.Windows.Controls.Separator());
            menu.Items.Add(miExit);
            ContextMenu = menu;
        }

        private void BuildUI() {
            _rootCard = new Border {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 0, 8, 0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Grid grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 核心要求 3: 歌词封面改用圆形的 (采用抗锯齿矢量 Ellipse + ImageBrush 实现完美正圆)
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
                VerticalAlignment = VerticalAlignment.Center
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

        // 核心要求 3: 封面旋转动效 (采用独立定时器驱动，杜绝 Visibility.Collapsed 导致动画脱轨故障)
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

            // 核心修复: 重新开启封面时彻底恢复可见性、边距及封面图片源
            if (_config.ShowCover) {
                _coverEllipse.Visibility = Visibility.Visible;
                _coverEllipse.Margin = new Thickness(0, 0, 8, 0);
                ImageSource curCover = (_engine.CurrentSong != null) 
                    ? _engine.GetCoverImage(_engine.CurrentSong.Id, _engine.CurrentSong.PicUrl) 
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
                _rootCard.Background = Brushes.Transparent;
            }

            UpdateRotationState();
            UpdateVisibility();
            _lastX = -9999;
            _lastY = -9999;
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

        // 核心全屏检测：智能识别 D3D 独占游戏、视频全屏、浏览器 F11 等全屏应用，杜绝歌词遮挡画面
        public bool IsFullscreenActive(IntPtr hTaskbar) {
            try {
                // 1. D3D 独占全屏与 PPT 放映检测 (系统级 API，零开销零误报)
                Win32.QUERY_USER_NOTIFICATION_STATE state;
                if (Win32.SHQueryUserNotificationState(out state) == 0) {
                    if (state == Win32.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                        state == Win32.QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE) {
                        return true;
                    }
                }

                if (hTaskbar == IntPtr.Zero) return false;

                // 2. 缓存优化：若前台窗口未变更且检查间隔未满 10 ticks (~300ms)，直接使用缓存判定，保持 0.00% CPU 占用
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

                // 3. 校验前台窗口是否为同屏幕上的全屏应用
                if (fg != IntPtr.Zero && fg != hTaskbar && fg != _hwnd) {
                    IntPtr fgMon = Win32.MonitorFromWindow(fg, Win32.MONITOR_DEFAULTTONULL);
                    if (fgMon == hMon && CheckWindowCoversMonitor(fg, mi.rcMonitor)) {
                        _lastIsFullscreen = true;
                        return true;
                    }
                }

                // 4. 任务栏上层覆盖检测（应对全屏视频播放时用户点击副屏导致前台窗口切换的情况）
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

            // 过滤 Windows 11 开始菜单、通知中心与系统弹窗
            if (cls == "Windows.UI.Core.CoreWindow" || cls == "XamlExplorerHostIslandWindow" || cls == "Shell_Dialog") {
                return false;
            }

            Win32.RECT rc;
            if (Win32.DwmGetWindowAttribute(hWnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out rc, Marshal.SizeOf(typeof(Win32.RECT))) != 0) {
                Win32.GetWindowRect(hWnd, out rc);
            }

            // 严格全屏几何覆盖：四边完全涵盖任务栏所在屏幕（允许 2px 误差）
            // 普通最大化窗口受工作区限制无法覆盖任务栏（底部相差 40px+），绝不会被误判
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

        // 核心层级自愈机制：轻量级检查歌词窗口是否不幸落入任务栏后方，若发现立即无闪烁提回置顶层顶端
        public void EnsureAboveTaskbar(IntPtr hTaskbar) {
            try {
                if (_hwnd == IntPtr.Zero || hTaskbar == IntPtr.Zero) return;

                bool isBehindTaskbar = false;

                // 沿 Z 序向上检查（GW_HWNDPREV）：若上方窗口链中有任务栏或其关联窗口，说明歌词被压在任务栏下方
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

                // 双向验证：若从任务栏向下链（GW_HWNDNEXT）能检索到歌词，亦说明歌词位于任务栏下方
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
                    // 核心关键：仅传 HWND_TOPMOST 对已经是 Topmost 的窗口无效！
                    // 必须先通过 HWND_TOPMOST 刷新置顶标志，再通过 HWND_TOP (IntPtr.Zero) 将其精准提升至置顶层最前端
                    Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                    Win32.SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
                }
            } catch { }
        }

        public bool UpdateVisibility(IntPtr hTaskbar, Win32.RECT rc, double dpiScaleY) {
            try {
                int screenH = Win32.GetSystemMetrics(1);
                int hideThreshold = Math.Max(4, (int)(8 * dpiScaleY));
                bool taskbarHidden = _config.AutoHideWithTaskbar && (rc.Top >= screenH - hideThreshold || rc.Bottom > screenH + hideThreshold);
                if (taskbarHidden) {
                    if (Visibility != Visibility.Hidden) Visibility = Visibility.Hidden;
                    return false;
                }

                // 全屏应用检测：有其他应用全屏时自动隐藏歌词，绝不覆盖游戏与视频画面
                if (_config.HideWhenFullscreen && IsFullscreenActive(hTaskbar)) {
                    if (Visibility != Visibility.Hidden) Visibility = Visibility.Hidden;
                    return false;
                }

                // 核心规则：波点音乐客户端退出或未启动时不显示（设置中心打开时保留预览；暂停播放时保持显示）
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

        // 核心要求 1: 杜绝任务栏点击闪烁与覆盖，高DPI物理高度精准对齐与垂直居中，具备断线重连与 Z 序自愈
        private void TaskbarFollowTimer_Tick(object sender, EventArgs e) {
            try {
                IntPtr hTaskbar = Win32.FindWindow("Shell_TrayWnd", null);
                if (hTaskbar == IntPtr.Zero || _hwnd == IntPtr.Zero) return;

                // 1. 动态宿主句柄维护：应对开机自启竞态、Explorer 重启或任务栏句柄变更
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

                // 2. 检查前台窗口：若用户刚刚点击了任务栏、桌面或系统托盘，立即进行自愈校验
                IntPtr fg = Win32.GetForegroundWindow();
                if (fg == hTaskbar || (fg != _lastFgHwnd && IsTaskbarWindow(fg, hTaskbar))) {
                    EnsureAboveTaskbar(hTaskbar);
                }

                // 3. 周期性轻量 Z-Order 自愈校验（每 3 个 tick 约 90ms 校验一次，发现被遮挡立即秒级提回）
                _zOrderCheckTick++;
                if (_zOrderCheckTick >= 3) {
                    _zOrderCheckTick = 0;
                    EnsureAboveTaskbar(hTaskbar);
                }

                // 物理高度匹配任务栏物理高度，targetY直接从任务栏顶边缘起算并加上YOffset微调
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

                // 如果位置与大小未变动，立刻跳过，绝不在 30ms 循环内无意义调用 SetWindowPos
                if (targetX == _lastX && targetY == _lastY && targetW == _lastW && targetH == _lastH) {
                    return;
                }

                _lastX = targetX;
                _lastY = targetY;
                _lastW = targetW;
                _lastH = targetH;

                // 不传 SWP_SHOWWINDOW，使用 SWP_NOACTIVATE | SWP_NOZORDER，彻底根治点击任务栏时的图层闪动
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
            });
        }

        private void Engine_OnCoverChanged(ImageSource cover) {
            Dispatcher.Invoke(() => {
                _coverBrush.ImageSource = cover != null ? cover : BodianEngine.DefaultCover;
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

    // ==========================================
    // 8. 纯白风控制中心与设置面板 (White Theme Settings UI)
    // ==========================================
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
        private Border _outerWindowBorder;
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
        private CapsuleSwitch _swAutoHide;
        private CapsuleSwitch _swHideWhenFullscreen;
        private CapsuleSwitch _swAutoStart;
        private CapsuleSwitch _swSilentStart;
        private CapsuleSwitch _swRunAsAdmin;

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
                ImageSource initCover = _engine.GetCoverImage(_engine.CurrentSong.Id, _engine.CurrentSong.PicUrl);
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
            Width = 800;
            Height = 670;
            MinWidth = 720;
            MinHeight = 600;
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
            if (_swAutoHide != null) _swAutoHide.ApplyTheme(isDark);
            if (_swHideWhenFullscreen != null) _swHideWhenFullscreen.ApplyTheme(isDark);
            if (_swAutoStart != null) _swAutoStart.ApplyTheme(isDark);
            if (_swRunAsAdmin != null) _swRunAsAdmin.ApplyTheme(isDark);

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
            // 核心要求 1: 纯白/深色大圆角外框，去除原生标题栏
            _outerWindowBorder = new Border {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 251)),
                CornerRadius = new CornerRadius(16),
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 228, 233)),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(14),
                Effect = new DropShadowEffect {
                    BlurRadius = 24,
                    ShadowDepth = 5,
                    Opacity = 0.16,
                    Color = Colors.Black
                }
            };

            Grid root = new Grid { Margin = new Thickness(22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Song card
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Options list
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer buttons

            // 1. 顶部 Header (支持按住拖动窗口，右侧内嵌最小化与关闭按钮)
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
                BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect {
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Opacity = 0.05,
                    Color = Colors.Black
                }
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
                Margin = new Thickness(0, 0, 14, 0),
                Effect = new DropShadowEffect {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = 0.12,
                    Color = Colors.Black
                }
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

            // 3. 中间区域：左侧选项卡导航 + 右侧独立分页内容
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
                Margin = new Thickness(0, 0, 14, 0),
                Effect = new DropShadowEffect {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = 0.03,
                    Color = Colors.Black
                }
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

            // Page 1: 📍 任务栏位置与微调
            ScrollViewer page1 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
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

            // 水平微调偏移 (默认值 12)
            _slXOffset = new Slider { Minimum = -50, Maximum = 150, TickFrequency = 1, IsSnapToTickEnabled = true };
            _tbXOffsetVal = new TextBlock();
            _slXOffset.ValueChanged += (s, e) => {
                _tbXOffsetVal.Text = string.Format("{0} px", (int)_slXOffset.Value);
                _config.XOffset = (int)_slXOffset.Value;
                _overlay.ApplyConfig(_config);
            };
            form1.Children.Add(CreateOptionCard("水平微调偏移", "", CreateSliderControl(_slXOffset, _tbXOffsetVal, 12, "{0} px")));

            // 垂直微调偏移 (默认值 0)
            _slYOffset = new Slider { Minimum = -25, Maximum = 25, TickFrequency = 1, IsSnapToTickEnabled = true };
            _tbYOffsetVal = new TextBlock();
            _slYOffset.ValueChanged += (s, e) => {
                int val = (int)_slYOffset.Value;
                _tbYOffsetVal.Text = string.Format("{0:+#;-#;0} px", val);
                _config.YOffset = val;
                _overlay.ApplyConfig(_config);
            };
            form1.Children.Add(CreateOptionCard("垂直微调偏移", "", CreateSliderControl(_slYOffset, _tbYOffsetVal, 0, "{0:+#;-#;0} px")));

            // 歌词显示宽度 (默认值 360, 范围 200~600)
            _slWidth = new Slider { Minimum = 200, Maximum = 600, TickFrequency = 5, IsSnapToTickEnabled = true };
            _tbWidthVal = new TextBlock();
            _slWidth.ValueChanged += (s, e) => {
                _tbWidthVal.Text = string.Format("{0} px", (int)_slWidth.Value);
                _config.Width = (int)_slWidth.Value;
                _overlay.ApplyConfig(_config);
            };
            form1.Children.Add(CreateOptionCard("歌词显示宽度", "", CreateSliderControl(_slWidth, _tbWidthVal, 360, "{0} px")));
            page1.Content = form1;

            // Page 2: 歌词动效
            ScrollViewer page2 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel form2 = new StackPanel();
            form2.Children.Add(CreateSectionHeader("✨ 歌词动效与过渡"));

            // 现代化下拉框：转场动效类型
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

            // 动效过渡时长 (默认值 250)
            _slAnimDuration = new Slider { Minimum = 100, Maximum = 600, TickFrequency = 50, IsSnapToTickEnabled = true };
            _tbAnimDurationVal = new TextBlock();
            _slAnimDuration.ValueChanged += (s, e) => {
                _tbAnimDurationVal.Text = string.Format("{0} ms", (int)_slAnimDuration.Value);
                _config.AnimationDurationMs = (int)_slAnimDuration.Value;
            };
            form2.Children.Add(CreateOptionCard("动效过渡时长", "", CreateSliderControl(_slAnimDuration, _tbAnimDurationVal, 250, "{0} ms")));
            page2.Content = form2;

            // Page 3: 封面、外观与个性化
            ScrollViewer page3 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel form3 = new StackPanel();
            form3.Children.Add(CreateSectionHeader("🎨 封面、外观与个性化设计"));

            // 控制中心色彩模式 (跟随系统 / 浅色明亮 / 深色暗黑)
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

            // 现代化下拉框：电脑已安装字体选择器
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

            // 2. 动态载入电脑中全部已安装字体（按名称排序并过滤 @ 竖排字体）
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
                _overlay.ApplyConfig(_config);
            };
            form3.Children.Add(CreateOptionCard("主歌词字号", "", CreateSliderControl(_slMainFontSize, _tbMainFontSizeVal, 15.0, "{0:F1} pt")));

            // 翻译副歌词字号 (默认 12.0, 范围 8.0~18.0)
            _slSubFontSize = new Slider { Minimum = 8.0, Maximum = 18.0, TickFrequency = 0.5, IsSnapToTickEnabled = true };
            _tbSubFontSizeVal = new TextBlock();
            _slSubFontSize.ValueChanged += (s, e) => {
                _tbSubFontSizeVal.Text = string.Format("{0:F1} pt", _slSubFontSize.Value);
                _config.SubFontSize = _slSubFontSize.Value;
                _overlay.ApplyConfig(_config);
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
                Margin = new Thickness(0, 0, 10, 0),
                Effect = new DropShadowEffect {
                    BlurRadius = 4,
                    ShadowDepth = 1,
                    Opacity = 0.2,
                    Color = Colors.Black
                }
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
            page3.Content = form3;

            // Page 4:  系统级联动与开机自启
            ScrollViewer page4 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel form4 = new StackPanel();
            form4.Children.Add(CreateSectionHeader("⚙️ 系统级联动与开机项"));

            _swAutoHide = new CapsuleSwitch();
            _swAutoHide.Checked += (s, e) => {
                _config.AutoHideWithTaskbar = true;
                _config.Save(_configPath);
                _overlay.ApplyConfig(_config);
            };
            _swAutoHide.Unchecked += (s, e) => {
                _config.AutoHideWithTaskbar = false;
                _config.Save(_configPath);
                _overlay.ApplyConfig(_config);
            };
            form4.Children.Add(CreateOptionCard("随任务栏自动隐藏", "任务栏隐藏时自动隐匿歌词", _swAutoHide));

            _swHideWhenFullscreen = new CapsuleSwitch();
            _swHideWhenFullscreen.Checked += (s, e) => {
                _config.HideWhenFullscreen = true;
                _config.Save(_configPath);
                _overlay.ApplyConfig(_config);
            };
            _swHideWhenFullscreen.Unchecked += (s, e) => {
                _config.HideWhenFullscreen = false;
                _config.Save(_configPath);
                _overlay.ApplyConfig(_config);
            };
            form4.Children.Add(CreateOptionCard("全屏时自动隐藏", "检测到游戏、视频或应用全屏时隐匿歌词", _swHideWhenFullscreen));

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
            form4.Children.Add(CreateOptionCard("默认以管理员权限运行 (推荐)", "避免播放器提权后进度不同步", _swRunAsAdmin));

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

            page4.Content = form4;

            // Page 5:  关于软件与开源项目
            ScrollViewer page5 = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
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
                Margin = new Thickness(0, 0, 0, 9),
                Effect = new DropShadowEffect {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = _isDarkTheme ? 0.25 : 0.03,
                    Color = Colors.Black
                }
            };
            _optionCards.Add(repoCard);

            StackPanel spRepo = new StackPanel();

            // 标题行与复制提示
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

            // 操作按钮行 (访问仓库 + 复制链接)
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
                Margin = new Thickness(0, 0, 0, 9),
                Effect = new DropShadowEffect {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = _isDarkTheme ? 0.25 : 0.03,
                    Color = Colors.Black
                }
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
                Text = "波点音乐专属任务栏歌词是一款轻量、极简且流畅的 Windows 任务栏歌词工具。支持波点音乐 PC 端播放状态与进度的实时同步、专辑封面黑胶动效、外文双行翻译以及高度个性化外观定制。",
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
                Margin = new Thickness(0, 0, 0, 9),
                Effect = new DropShadowEffect {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = _isDarkTheme ? 0.25 : 0.03,
                    Color = Colors.Black
                }
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

            page5.Content = form5;

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

            // 4. 底部操作栏 (全部改用圆角矩形按钮)
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

            //  2: 保存设置在旁边提示即可，去除弹窗
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
            btnSave.Click += (s, e) => {
                SaveValuesToConfig();
                _overlay.ApplyConfig(_config);

                // 平滑淡入提示并在 2.5 秒后平滑淡出
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
            Content = _outerWindowBorder;
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
                Margin = new Thickness(0, 0, 0, 9),
                Effect = new DropShadowEffect {
                    BlurRadius = 8,
                    ShadowDepth = 1,
                    Opacity = _isDarkTheme ? 0.25 : 0.03,
                    Color = Colors.Black
                }
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

        // 1: 拖动条默认值指示点及一键快速重置
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

            Action updateBadgeStyle = () => {
                bool isDef = Math.Abs(slider.Value - defaultVal) < 0.01;
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

            _badgeUpdaters.Add(updateBadgeStyle);

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

            _swAutoHide.SetChecked(_config.AutoHideWithTaskbar, false);
            _swHideWhenFullscreen.SetChecked(_config.HideWhenFullscreen, false);
            _swAutoStart.SetChecked(Win32.IsAutoStartEnabled(), false);
            _swSilentStart.SetChecked(_config.SilentStart, false);
            _swRunAsAdmin.SetChecked(_config.RunAsAdmin, false);
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
            _config.AutoHideWithTaskbar = (_swAutoHide.IsChecked == true);
            _config.HideWhenFullscreen = (_swHideWhenFullscreen.IsChecked == true);
            _config.SilentStart = (_swSilentStart.IsChecked == true);
            _config.RunAsAdmin = (_swRunAsAdmin.IsChecked == true);

            Win32.SetAutoStart(_swAutoStart.IsChecked == true);
            _config.Save(_configPath);
        }
    }

    // ==========================================
    // 9. 应用程序主入口 (Program Main)
    // ==========================================
    public class Program {
        private static Mutex _appMutex;
        private static NotifyIcon _trayIcon;
        private static EventWaitHandle _wakeEvent;
        private static EventWaitHandle _exitEvent;
        private const string WAKE_EVENT_NAME = @"Global\BodianTaskbarLyric_WakeEvent_2026";
        private const string EXIT_EVENT_NAME = @"Global\BodianTaskbarLyric_ExitEvent_2026";
        private const string MUTEX_NAME = @"Global\BodianTaskbarLyric_Unique_Mutex_2026";
        [ThreadStatic]
        private static bool _inLog;
        private static string _appDir;

        public static string AppDir {
            get {
                if (_appDir == null) {
                    try {
                        string loc = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        if (!string.IsNullOrEmpty(loc)) _appDir = Path.GetDirectoryName(loc);
                    } catch { }
                    if (string.IsNullOrEmpty(_appDir)) _appDir = AppDomain.CurrentDomain.BaseDirectory;
                }
                return _appDir;
            }
        }

        public static void Log(string msg) {
            if (_inLog) return;
            try {
                _inLog = true;
                File.AppendAllText(Path.Combine(AppDir, "run.log"), string.Format("[{0:HH:mm:ss.fff}] {1}\r\n", DateTime.Now, msg));
            } catch { }
            finally {
                _inLog = false;
            }
        }

        public static void ActivateSettingsWindow(SettingsWindow settingsWin) {
            try {
                if (settingsWin == null) return;
                settingsWin.Show();
                if (settingsWin.WindowState == WindowState.Minimized) {
                    settingsWin.WindowState = WindowState.Normal;
                }
                settingsWin.Activate();
                settingsWin.Topmost = true;
                settingsWin.Topmost = false;
                settingsWin.Focus();

                IntPtr hWnd = new WindowInteropHelper(settingsWin).Handle;
                if (hWnd != IntPtr.Zero) {
                    Win32.SwitchToThisWindow(hWnd, true);
                    Win32.SetForegroundWindow(hWnd);
                }
            } catch (Exception ex) {
                Log("ActivateSettingsWindow error: " + ex.Message);
            }
        }

        [STAThread]
        public static void Main(string[] args) {
            try {
                // 支持命令行退出指令
                if (args != null && args.Length > 0 && (args[0].Equals("--exit", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-exit", StringComparison.OrdinalIgnoreCase))) {
                    try {
                        using (var exitWait = EventWaitHandle.OpenExisting(EXIT_EVENT_NAME)) {
                            exitWait.Set();
                            Log("Signaled running instance to exit via CLI.");
                            return;
                        }
                    } catch { return; }
                }

                AppDomain.CurrentDomain.ProcessExit += (s, e) => {
                    Log("PROCESS EXIT EVENT FIRED! Stack:\r\n" + Environment.StackTrace);
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                    Log("UNHANDLED EXCEPTION: " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null"));
                };

                Log("Program starting v" + AppConfig.APP_VERSION + "...");

                // 核心多开唤醒机制：若程序已在后台运行，直接通过全局命名事件唤醒设置窗口，无需经过UAC提示，零延迟秒开
                try {
                    using (var wake = EventWaitHandle.OpenExisting(WAKE_EVENT_NAME)) {
                        wake.Set();
                        Log("Signaled running instance to wake up.");
                        return;
                    }
                } catch (WaitHandleCannotBeOpenedException) {
                    // 没有已有实例在运行，正常启动
                } catch (Exception ex) {
                    Log("Wake check: " + ex.Message);
                }

                string appDir = AppDir;
                string configPath = Path.Combine(appDir, "config.json");
                AppConfig config = AppConfig.Load(configPath);
                Log("Config loaded (v" + config.Version + ").");

                // 核心特性：默认以管理员权限运行开关
                if (config.RunAsAdmin && !Win32.IsAdministrator() && (args == null || Array.IndexOf(args, "--no-elevation") < 0)) {
                    try {
                        ProcessStartInfo psi = new ProcessStartInfo {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            WorkingDirectory = appDir,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        Log("Elevated instance launched, exiting unprivileged process.");
                        return;
                    } catch (Exception uacEx) {
                        Log("User cancelled UAC elevation: " + uacEx.Message);
                    }
                }

                // 创建跨权限、跨会话的唤醒与退出 EventWaitHandle 与互斥体
                bool isNewInstance;
                EventWaitHandleSecurity sec = new EventWaitHandleSecurity();
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                sec.AddAccessRule(new EventWaitHandleAccessRule(everyone, EventWaitHandleRights.FullControl, AccessControlType.Allow));
                _wakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WAKE_EVENT_NAME, out isNewInstance, sec);

                if (!isNewInstance) {
                    Log("WakeEvent already exists, signaling existing instance.");
                    _wakeEvent.Set();
                    return;
                }

                bool isExitNew;
                _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EXIT_EVENT_NAME, out isExitNew, sec);
                _exitEvent.Reset();

                _appMutex = new Mutex(true, MUTEX_NAME, out isNewInstance);
                Log("Mutex acquired: " + isNewInstance);
                if (!isNewInstance) {
                    Log("Mutex already held, exiting.");
                    return;
                }

                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += (s, e) => {
                    Log("DISPATCHER EXCEPTION: " + e.Exception.ToString());
                    e.Handled = true;
                };

                // 1. 同步预加载数据库最新记录
                BodianEngine engine = new BodianEngine();
                engine.Init();
                Log("Engine inited.");

                SettingsWindow settingsWin = null;

                // 2. 初始化任务栏浮窗与控制中心
                TaskbarOverlayWindow overlay = new TaskbarOverlayWindow(config, configPath, engine, () => {
                    if (settingsWin != null) {
                        ActivateSettingsWindow(settingsWin);
                    }
                });
                Log("Overlay created.");

                settingsWin = new SettingsWindow(config, configPath, engine, overlay);
                Log("SettingsWindow created.");
                overlay.SetSettingsWindowOpen(settingsWin.IsVisible && settingsWin.WindowState != WindowState.Minimized);

                // 启动后台线程监听唤醒事件
                Thread wakeThread = new Thread(() => {
                    while (true) {
                        try {
                            if (_wakeEvent.WaitOne()) {
                                Log("Wake event received, activating SettingsWindow...");
                                if (Application.Current != null && Application.Current.Dispatcher != null) {
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                                        ActivateSettingsWindow(settingsWin);
                                    }));
                                }
                            }
                        } catch (ThreadAbortException) {
                            break;
                        } catch (Exception ex) {
                            Log("Wake thread error: " + ex.Message);
                        }
                    }
                });
                wakeThread.IsBackground = true;
                wakeThread.Start();

                // 启动后台线程监听退出事件
                Thread exitThread = new Thread(() => {
                    while (true) {
                        try {
                            if (_exitEvent.WaitOne()) {
                                Log("Exit event received, shutting down application...");
                                if (Application.Current != null && Application.Current.Dispatcher != null) {
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                                        Application.Current.Shutdown();
                                    }));
                                }
                                break;
                            }
                        } catch (ThreadAbortException) {
                            break;
                        } catch { }
                    }
                });
                exitThread.IsBackground = true;
                exitThread.Start();

                InitTrayIcon(settingsWin);
                Log("TrayIcon created.");

                overlay.Show();
                if (!config.SilentStart) {
                    settingsWin.Show();
                }
                Log("Windows shown (SilentStart=" + config.SilentStart + ").");

                // 3. 启动后台线程监听
                engine.Start();
                Log("Engine started, entering app.Run()...");

                app.Run();
                Log("app.Run() returned.");

                if (_trayIcon != null) {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            } catch (Exception ex) {
                Log("EXCEPTION: " + ex.ToString());
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_err.log"), ex.ToString());
            }
        }

        private static void InitTrayIcon(SettingsWindow settingsWin) {
            try {
                _trayIcon = new NotifyIcon();
                _trayIcon.Text = "波点音乐 - 任务栏歌词 v" + AppConfig.APP_VERSION;
                try {
                    string appIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "app.ico");
                    if (!File.Exists(appIco)) appIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                    string icoPng = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "ico.png");
                    if (!File.Exists(icoPng)) icoPng = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ico.png");
                    if (File.Exists(appIco)) {
                        _trayIcon.Icon = new System.Drawing.Icon(appIco);
                    } else if (File.Exists(icoPng)) {
                        using (var bmp = new System.Drawing.Bitmap(icoPng)) {
                            IntPtr hIcon = bmp.GetHicon();
                            _trayIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
                        }
                    } else {
                        _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                    }
                } catch {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                _trayIcon.Visible = true;

                _trayIcon.DoubleClick += (s, e) => {
                    ActivateSettingsWindow(settingsWin);
                };

                ContextMenuStrip trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("⚙️ 打开控制中心", null, (s, e) => {
                    ActivateSettingsWindow(settingsWin);
                });
                trayMenu.Items.Add(new ToolStripSeparator());
                trayMenu.Items.Add("❌ 退出程序", null, (s, e) => {
                    Application.Current.Shutdown();
                });

                _trayIcon.ContextMenuStrip = trayMenu;
            } catch { }
        }
    }
}
