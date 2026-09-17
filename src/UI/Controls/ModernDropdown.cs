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
                Padding = new Thickness(4)
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

}
