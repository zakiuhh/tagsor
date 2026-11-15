using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Tagsor
{
    public partial class NamePromptDialog : Window
    {
        public string EnteredName { get; private set; } = "";
        public string SelectedAccentHex { get; private set; } = "#60A5FA";

        private static readonly (string Hex, string Label)[] PresetColors =
        {
            ("#60A5FA", "Sky Blue"),
            ("#34D399", "Emerald"),
            ("#FBBF24", "Amber"),
            ("#F87171", "Coral"),
            ("#C084FC", "Violet")
        };

        private readonly List<Border> _swatchRings = new();
        private Brush _activeRingBrush = Brushes.White;
        private bool _isUpdatingHexProgrammatically = false;
        private bool _isClosing = false;

        public NamePromptDialog(
            string title = "New mouse detected",
            string initialName = "",
            string subtitle = "Give this mouse a name and accent color.",
            string initialAccentHex = "#60A5FA")
        {
            InitializeComponent();

            TitleText.Text = title;
            SubtitleText.Text = subtitle;
            NameInputBox.Text = initialName;

            SelectedAccentHex = NormalizeHex(initialAccentHex) ?? "#60A5FA";

            SyncWithWindowsTheme();
            BuildPresetSwatches();
            ApplyAccentColor(SelectedAccentHex, updateTextBox: true);

            StateChanged += OnWindowStateChanged;

            Loaded += (s, e) =>
            {
                NameInputBox.Focus();
                NameInputBox.SelectAll();
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    TrySave();
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    CloseSmoothly(false);
                }
            };
        }

        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                OuterMarginGrid.Margin = new Thickness(8);
                RootBorder.CornerRadius = new CornerRadius(0);
                MaximizeGlyph.Data = Geometry.Parse("M 7,4 H 20 V 17 M 4,7 H 17 V 20 H 4 Z");
            }
            else
            {
                OuterMarginGrid.Margin = new Thickness(14);
                RootBorder.CornerRadius = new CornerRadius(15);
                MaximizeGlyph.Data = Geometry.Parse("M 5,5 H 19 V 19 H 5 Z");
            }
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized)
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                MaximizeRestore_Click(sender, e);
                return;
            }

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                if (WindowState == WindowState.Maximized)
                {
                    var mousePos = PointToScreen(e.GetPosition(this));
                    WindowState = WindowState.Normal;
                    Left = mousePos.X - (Width / 2);
                    Top = Math.Max(0, mousePos.Y - 20);
                }
                DragMove();
            }
        }

        private void BuildPresetSwatches()
        {
            PresetSwatchesPanel.Children.Clear();
            _swatchRings.Clear();

            foreach (var (hex, label) in PresetColors)
            {
                var ring = new Border
                {
                    Width = 28,
                    Height = 28,
                    Margin = new Thickness(0, 0, 6, 0),
                    CornerRadius = new CornerRadius(14),
                    BorderThickness = new Thickness(2),
                    BorderBrush = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    ToolTip = $"{label} ({hex})",
                    Tag = hex,
                    Child = new Ellipse
                    {
                        Width = 18,
                        Height = 18,
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
                    }
                };

                ring.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    ApplyAccentColor(hex, updateTextBox: true);
                };

                _swatchRings.Add(ring);
                PresetSwatchesPanel.Children.Add(ring);
            }
        }

        private void HexInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingHexProgrammatically || HexValidationHint == null) return;

            string raw = HexInputBox.Text.Trim();
            string? validHex = NormalizeHex(raw);

            if (validHex != null)
            {
                HexValidationHint.Text = "Valid hex";
                HexValidationHint.Foreground = (Brush)Resources["SecondaryTextBrush"];
                ApplyAccentColor(validHex, updateTextBox: false);
            }
            else
            {
                HexValidationHint.Text = "Use #RRGGBB";
                HexValidationHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FB7185"));
                UpdateSwatchSelectionHighlight(null);
            }
        }

        private void ApplyAccentColor(string hex, bool updateTextBox)
        {
            SelectedAccentHex = hex;
            if (LivePreviewSwatch == null || SaveBtn == null) return;

            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);

            LivePreviewSwatch.Background = brush;
            SaveBtn.Background = brush;

            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            SaveBtn.Foreground = luminance > 0.62
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F1117"))
                : Brushes.White;

            if (updateTextBox && HexInputBox != null && HexValidationHint != null)
            {
                _isUpdatingHexProgrammatically = true;
                HexInputBox.Text = hex;
                _isUpdatingHexProgrammatically = false;
                HexValidationHint.Text = "Valid hex";
                HexValidationHint.Foreground = (Brush)Resources["SecondaryTextBrush"];
            }

            UpdateSwatchSelectionHighlight(hex);
        }

        private void UpdateSwatchSelectionHighlight(string? activeHex)
        {
            foreach (var ring in _swatchRings)
            {
                string swatchHex = ring.Tag?.ToString() ?? "";
                ring.BorderBrush = (!string.IsNullOrEmpty(activeHex) && string.Equals(swatchHex, activeHex, StringComparison.OrdinalIgnoreCase))
                    ? _activeRingBrush
                    : Brushes.Transparent;
            }
        }

        private static string? NormalizeHex(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            string cleaned = input.Trim();
            if (!cleaned.StartsWith("#")) cleaned = "#" + cleaned;

            if (Regex.IsMatch(cleaned, @"^#[0-9A-Fa-f]{3}$"))
            {
                cleaned = $"#{cleaned[1]}{cleaned[1]}{cleaned[2]}{cleaned[2]}{cleaned[3]}{cleaned[3]}";
            }

            return Regex.IsMatch(cleaned, @"^#[0-9A-Fa-f]{6}$")
                ? cleaned.ToUpperInvariant()
                : null;
        }

        private void NameInputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SaveBtn == null) return;
            SaveBtn.IsEnabled = !string.IsNullOrWhiteSpace(NameInputBox.Text);
            SaveBtn.Opacity = SaveBtn.IsEnabled ? 1.0 : 0.45;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e) => TrySave();

        private void TrySave()
        {
            string trimmed = NameInputBox.Text.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                NameInputBox.Focus();
                return;
            }

            string? finalHex = NormalizeHex(HexInputBox.Text);
            if (finalHex != null)
            {
                SelectedAccentHex = finalHex;
            }

            EnteredName = trimmed;
            CloseSmoothly(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => CloseSmoothly(false);

        private void SyncWithWindowsTheme()
        {
            bool isLight = false;
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int useLight)
                {
                    isLight = (useLight == 1);
                }
            }
            catch { }

            if (isLight)
            {
                Resources["ScrollThumbBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C4C4CE"));
                Resources["ScrollThumbHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9E9EAA"));
                Resources["CardBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F7FA"));
                Resources["CardBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4DC"));
                Resources["SectionBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                Resources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                Resources["SecondaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
                Resources["InputBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                Resources["InputBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8C8D2"));
                Resources["BtnBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECF2"));
                Resources["BtnHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E8"));
                Resources["BtnBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D0DA"));
                Resources["CaptionHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4EA"));
                Resources["CaptionIconBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
                _activeRingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                CardShadow.Opacity = 0.22;
            }
            else
            {
                _activeRingBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4F4F6"));
            }
        }

        private void CloseSmoothly(bool result)
        {
            if (_isClosing) return;
            _isClosing = true;

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(140));
            var scaleOut = new DoubleAnimation(1.0, 0.96, TimeSpan.FromMilliseconds(140));

            fadeOut.Completed += (s, e) =>
            {
                DialogResult = result;
                Close();
            };

            RootBorder.BeginAnimation(OpacityProperty, fadeOut);
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
        }
    }
}
