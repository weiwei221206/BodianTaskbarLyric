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
                BorderThickness = new Thickness(0.8),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 0, 0, 0))
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
            if (_thumb != null) {
                _thumb.BorderBrush = _isDark ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) : new SolidColorBrush(Color.FromArgb(45, 0, 0, 0));
            }
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

}
