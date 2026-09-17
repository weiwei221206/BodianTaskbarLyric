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

}
