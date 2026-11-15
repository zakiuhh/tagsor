using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Tagsor
{
    public partial class TrayMenuPopup : Window
    {
        public event Action<string>? OnSwitchMouseRequested;
        public event Action<string>? OnDeleteMouseRequested;
        public event Action? OnEditMouseRequested;
        public event Action? OnResetMiceRequested;
        public event Action? OnToggleSpotlightRequested;
        public event Action? OnToggleMarkerRequested;
        public event Action<string, string>? OnStatusChipRequested;
        public event Action? OnTogglePrivacyRequested;
        public event Action? OnTogglePauseRequested;
        public event Action? OnOpenSettingsRequested;
        public event Action? OnExitRequested;

        private bool _closed = false;

        public TrayMenuPopup(
            string activeUserName,
            string accentHex,
            string activeDeviceId,
            IReadOnlyDictionary<string, MouseProfile> savedMice,
            AppSettings settings,
            bool isPrivacyMode,
            bool isPaused,
            bool isSpotlightActive,
            bool isMarkerActive)
        {
            InitializeComponent();

            ActiveUserBadge.Text = activeUserName;
            PrivacyCheck.Visibility = isPrivacyMode ? Visibility.Visible : Visibility.Collapsed;
            PauseCheck.Visibility = isPaused ? Visibility.Visible : Visibility.Collapsed;
            SpotlightCheck.Visibility = isSpotlightActive ? Visibility.Visible : Visibility.Collapsed;
            MarkerCheck.Visibility = isMarkerActive ? Visibility.Visible : Visibility.Collapsed;

            SpotlightShortcutLabel.Text = settings.SpotlightHotkey;
            MarkerShortcutLabel.Text = settings.MarkerHotkey;
            PrivacyShortcutLabel.Text = settings.PrivacyHotkey;
            Chip1Btn.ToolTip = settings.Status1Hotkey;
            Chip2Btn.ToolTip = settings.Status2Hotkey;
            Chip3Btn.ToolTip = settings.Status3Hotkey;

            try
            {
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isPaused ? "#FBBF24" : accentHex));
            }
            catch
            {
                StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60A5FA"));
            }

            StatusText.Text = isPaused ? "Paused" : (isMarkerActive ? "Drawing" : "Active");

            PopulateSavedMiceList(activeDeviceId, savedMice);
            SyncWithWindowsTheme();

            Deactivated += (s, e) => SafeClose();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) SafeClose();
            };
        }

        private void PopulateSavedMiceList(string activeDeviceId, IReadOnlyDictionary<string, MouseProfile> savedMice)
        {
            if (savedMice.Count == 0) return;

            SavedMicePanel.Visibility = Visibility.Visible;
            SavedMiceDivider.Visibility = Visibility.Visible;

            var headerLabel = new TextBlock
            {
                Text = "Saved mice",
                FontSize = 10.5,
                Margin = new Thickness(10, 2, 10, 3)
            };
            headerLabel.SetResourceReference(TextBlock.ForegroundProperty, "MutedTextBrush");
            SavedMicePanel.Children.Add(headerLabel);

            foreach (var kvp in savedMice)
            {
                string deviceId = kvp.Key;
                MouseProfile profile = kvp.Value;
                bool isCurrent = string.Equals(deviceId, activeDeviceId, StringComparison.OrdinalIgnoreCase);

                var rowGrid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var switchBtn = new Button
                {
                    Style = (Style)Resources["MenuActionItem"],
                    Padding = new Thickness(10, 6, 8, 6),
                    ToolTip = $"Switch to {profile.Name} ({deviceId})"
                };

                var itemContent = new Grid();
                var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                Color dotColor;
                try { dotColor = (Color)ColorConverter.ConvertFromString(profile.AccentHex); }
                catch { dotColor = (Color)ColorConverter.ConvertFromString("#60A5FA"); }

                leftStack.Children.Add(new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = new SolidColorBrush(dotColor),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                leftStack.Children.Add(new TextBlock
                {
                    Text = profile.Name,
                    FontSize = 12.5,
                    FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                    VerticalAlignment = VerticalAlignment.Center
                });

                itemContent.Children.Add(leftStack);

                if (isCurrent)
                {
                    itemContent.Children.Add(new TextBlock
                    {
                        Text = "✓",
                        Foreground = new SolidColorBrush(dotColor),
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                switchBtn.Content = itemContent;
                switchBtn.Click += (s, e) =>
                {
                    SafeClose();
                    OnSwitchMouseRequested?.Invoke(deviceId);
                };

                var deleteBtn = new Button
                {
                    Style = (Style)Resources["DangerMenuItem"],
                    Padding = new Thickness(7, 5, 7, 5),
                    Margin = new Thickness(2, 1, 0, 1),
                    ToolTip = $"Remove {profile.Name}",
                    Content = new TextBlock
                    {
                        Text = "×",
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                deleteBtn.Click += (s, e) =>
                {
                    SafeClose();
                    OnDeleteMouseRequested?.Invoke(deviceId);
                };

                Grid.SetColumn(switchBtn, 0);
                Grid.SetColumn(deleteBtn, 1);
                rowGrid.Children.Add(switchBtn);
                rowGrid.Children.Add(deleteBtn);
                SavedMicePanel.Children.Add(rowGrid);
            }
        }

        public void PositionNearCursor(double cursorDipX, double cursorDipY)
        {
            Loaded += (s, e) =>
            {
                var workArea = SystemParameters.WorkArea;
                double left = cursorDipX - (ActualWidth / 2);
                double top = cursorDipY - ActualHeight + 6;

                if (left + ActualWidth > workArea.Right) left = workArea.Right - ActualWidth;
                if (left < workArea.Left) left = workArea.Left;
                if (top < workArea.Top) top = cursorDipY;

                Left = left;
                Top = top;
            };
        }

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
                Resources["MenuBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F8FA"));
                Resources["MenuBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4DC"));
                Resources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                Resources["MutedTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#71717A"));
                Resources["ItemHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECF2"));
                Resources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4EC"));
                Resources["DangerHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                MenuShadow.Opacity = 0.22;
            }
            else
            {
                Resources["MenuBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28282D"));
                Resources["MenuBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42424A"));
                Resources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F5"));
                Resources["MutedTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E98"));
                Resources["ItemHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#36363F"));
                Resources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383840"));
                Resources["DangerHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#45272B"));
                MenuShadow.Opacity = 0.55;
            }
        }

        private void SafeClose()
        {
            if (_closed) return;
            _closed = true;
            Close();
        }

        private void EditMouse_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnEditMouseRequested?.Invoke();
        }

        private void ResetMice_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnResetMiceRequested?.Invoke();
        }

        private void Spotlight_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnToggleSpotlightRequested?.Invoke();
        }

        private void Marker_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnToggleMarkerRequested?.Invoke();
        }

        private void Chip1_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnStatusChipRequested?.Invoke("👀 Look here", "#38BDF8");
        }

        private void Chip2_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnStatusChipRequested?.Invoke("⏸ Hold on", "#F59E0B");
        }

        private void Chip3_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnStatusChipRequested?.Invoke("✅ Done", "#34D399");
        }

        private void Privacy_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnTogglePrivacyRequested?.Invoke();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnTogglePauseRequested?.Invoke();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnOpenSettingsRequested?.Invoke();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnExitRequested?.Invoke();
        }
    }
}
