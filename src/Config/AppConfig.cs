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
    public class AppConfig {
        public const string APP_VERSION = "1.1.2";
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
        public bool DisableHardwareAcceleration = true;

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
                            if (b.ContainsKey("disable_hardware_acceleration")) cfg.DisableHardwareAcceleration = Convert.ToBoolean(b["disable_hardware_acceleration"]);
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
                    { "silent_start", SilentStart },
                    { "disable_hardware_acceleration", DisableHardwareAcceleration }
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

}
