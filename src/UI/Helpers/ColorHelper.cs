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

}
