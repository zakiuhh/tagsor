using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Tagsor
{
    public partial class CursorOverlay : Window
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private IntPtr _hwnd;
        private bool _isLightTheme = false;
        private bool _isFadedOut = false;
        private bool _isShakeSurging = false;
        private string _accentHex = "#60A5FA";
        private Brush _currentForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFEFEF"));

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public CursorOverlay(string userName)
        {
            InitializeComponent();
            UserNameLabel.Text = userName;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            // Omitted WS_EX_TOOLWINDOW so screen recorders capture the overlay
            SetWindowLong(_hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
            EnforceTopmost();
        }

        public void EnforceTopmost()
        {
            if (_hwnd != IntPtr.Zero)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }

        public void SetAccentColor(string hexColor)
        {
            _accentHex = hexColor;
            ApplyRestBorder();
        }

        private void ApplyRestBorder()
        {
            try
            {
                var accentColor = (Color)ColorConverter.ConvertFromString(_accentHex);
                accentColor.A = _isLightTheme ? (byte)150 : (byte)130;
                PillBorder.BorderBrush = new SolidColorBrush(accentColor);
            }
            catch
            {
                PillBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_isLightTheme ? "#D0D0D7" : "#42424A"));
            }
        }

        // 1A. Click Ripple + Double-Click Pulse
        public void TriggerClickAnimation(bool isPressed, string buttonType = "LMB", bool isDoubleClick = false)
                {
                    if (isPressed)
                    {
                        string activeHex = buttonType switch
                        {
                            "RMB" => "#F59E0B",
                            "MMB" => "#F472B6",
                            _ => _accentHex
                        };

                        PillBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(activeHex));

                        // Pill press compression (or double-click pop)
                        double targetScale = isDoubleClick ? 1.16 : (buttonType == "MMB" ? 0.85 : 0.91);
                        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
                        var scaleAnim = new DoubleAnimation(targetScale, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease };
                        PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                        PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                    }
                    else
                    {
                        SetHoldIndicator(null);
                        if (!_isShakeSurging)
                        {
                            var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };
                            var scaleAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140)) { EasingFunction = ease };
                            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                        }
                        ApplyRestBorder();
                    }
                }

        // 1B. Drag-Hold Badge (LMB / RMB / MMB)
        public void SetHoldIndicator(string? buttonLabel)
        {
            if (string.IsNullOrEmpty(buttonLabel))
            {
                HoldBadge.Visibility = Visibility.Collapsed;
                return;
            }

            string badgeHex = buttonLabel switch
            {
                "RMB" => "#F59E0B",
                "MMB" => "#F472B6",
                _ => _accentHex
            };

            HoldBadgeText.Text = buttonLabel;
            HoldBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(badgeHex));
            HoldBadge.Visibility = Visibility.Visible;
        }

        // 5. Shake-to-Locate Surge
        public void TriggerShakeSurge()
        {
            if (_isShakeSurging) return;
            _isShakeSurging = true;

            SetIdleState(false);

            var accentBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_accentHex));
            PillBorder.BorderBrush = accentBrush;

            var surgeUp = new DoubleAnimation(1.38, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            };
            surgeUp.Completed += (s, e) =>
            {
                var settleDown = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(380))
                {
                    BeginTime = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                settleDown.Completed += (_, _) =>
                {
                    _isShakeSurging = false;
                    ApplyRestBorder();
                };
                PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, settleDown);
                PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, settleDown);
            };

            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, surgeUp);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, surgeUp);
        }

        public void TriggerScrollBounce(int wheelDelta)
        {
            double offset = wheelDelta > 0 ? -3.5 : 3.5;
            var bounceAnim = new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            PillTranslate.BeginAnimation(TranslateTransform.YProperty, bounceAnim);
        }

        // 4. Keystrokes + Repeat Counter (×2, ×3)
        public void ShowTypedKeys(List<KeyDisplayItem> keys)
        {
            UserNameLabel.Visibility = Visibility.Collapsed;
            KeysPanel.Visibility = Visibility.Visible;
            KeysPanel.Children.Clear();

            for (int i = 0; i < keys.Count; i++)
            {
                var item = keys[i];

                if (item.HasIcon)
                {
                    var canvas = new Canvas { Width = 24, Height = 24 };
                    var path = new Path
                    {
                        Data = Geometry.Parse(item.IconGeometry!),
                        Stroke = _currentForeground,
                        StrokeThickness = 1.85,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        SnapsToDevicePixels = true
                    };
                    canvas.Children.Add(path);

                    KeysPanel.Children.Add(new Viewbox
                    {
                        Width = 14,
                        Height = 14,
                        Stretch = Stretch.Uniform,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = canvas
                    });
                }
                else
                {
                    KeysPanel.Children.Add(new TextBlock
                    {
                        Text = item.Label,
                        Foreground = _currentForeground,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 12.5,
                        FontWeight = FontWeights.Medium,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                // Repeat badge (e.g., ×3)
                if (item.RepeatCount > 1)
                {
                    var repeatScale = new ScaleTransform(1, 1);
                    var badge = new Border
                    {
                        Margin = new Thickness(4, 0, 0, 0),
                        Padding = new Thickness(4, 0.5, 4, 0.5),
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_accentHex)),
                        RenderTransformOrigin = new Point(0.5, 0.5),
                        RenderTransform = repeatScale,
                        Child = new TextBlock
                        {
                            Text = $"×{item.RepeatCount}",
                            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#111115")),
                            FontFamily = new FontFamily("Segoe UI"),
                            FontSize = 10.5,
                            FontWeight = FontWeights.Bold
                        }
                    };

                    if (i == keys.Count - 1)
                    {
                        var pop = new DoubleAnimation(1.25, 1.0, TimeSpan.FromMilliseconds(140));
                        repeatScale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                        repeatScale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
                    }

                    KeysPanel.Children.Add(badge);
                }

                if (i < keys.Count - 1)
                {
                    KeysPanel.Children.Add(new TextBlock
                    {
                        Text = " · ",
                        Foreground = _currentForeground,
                        Opacity = 0.5,
                        FontSize = 12.5,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
            }
        }

        // 3. Status Chip / User Name Display
        public void ShowStatusChip(string statusText, string accentHex)
        {
            KeysPanel.Visibility = Visibility.Collapsed;
            UserNameLabel.Visibility = Visibility.Visible;
            UserNameLabel.Text = statusText;
            PillBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentHex));

            var popAnim = new DoubleAnimation(1.14, 1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 5 }
            };
            PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, popAnim);
            PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, popAnim);
        }

        public void SetUserNameAnimated(string newName, string accentHex, bool forceShowName = false)
        {
            _accentHex = accentHex;
            ApplyRestBorder();

            bool wasShowingKeys = KeysPanel.Visibility == Visibility.Visible;
            if (!forceShowName && !wasShowingKeys && UserNameLabel.Text == newName) return;

            KeysPanel.Visibility = Visibility.Collapsed;
            UserNameLabel.Visibility = Visibility.Visible;
            UserNameLabel.Text = newName;

            if (!_isShakeSurging)
            {
                var popAnim = new DoubleAnimation(1.12, 1.0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 5 }
                };
                PillScale.BeginAnimation(ScaleTransform.ScaleXProperty, popAnim);
                PillScale.BeginAnimation(ScaleTransform.ScaleYProperty, popAnim);
            }
        }

        public void SetTiltFromVelocity(double deltaX)
        {
            double clampedAngle = Math.Clamp(deltaX * 0.25, -6.0, 6.0);
            var tiltAnim = new DoubleAnimation(clampedAngle, TimeSpan.FromMilliseconds(100));
            PillRotate.BeginAnimation(RotateTransform.AngleProperty, tiltAnim);
        }

        public void SetIdleState(bool isIdle)
        {
            if (_isFadedOut == isIdle) return;
            _isFadedOut = isIdle;

            if (isIdle)
            {
                var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(400));
                this.BeginAnimation(Window.OpacityProperty, fadeOut);
            }
            else
            {
                this.BeginAnimation(Window.OpacityProperty, null);
                this.Opacity = 1.0;
            }
        }

        public void ApplyTheme(bool isLightTheme)
        {
            _isLightTheme = isLightTheme;
            if (isLightTheme)
            {
                PillBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F7F9"));
                _currentForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E22"));
                PillShadow.Opacity = 0.22;
            }
            else
            {
                PillBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B2B30"));
                _currentForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFEFEF"));
                PillShadow.Opacity = 0.45;
            }
            UserNameLabel.Foreground = _currentForeground;
            ApplyRestBorder();
        }
    }
}
