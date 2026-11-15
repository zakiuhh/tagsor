using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace Tagsor
{
    public partial class SettingsWindow : Window
    {
        public AppSettings UpdatedSettings { get; private set; }
        public Dictionary<string, MouseProfile> WorkingMice { get; private set; }
        public string ActiveDeviceId { get; private set; }
        public bool LaunchOnStartup { get; private set; }

        private readonly string _appDataFolder;
        private string _previousHotkeyText = "";
        private bool _isClosing = false;

        public SettingsWindow(
            AppSettings currentSettings,
            IReadOnlyDictionary<string, MouseProfile> savedMice,
            string activeDeviceId,
            bool launchOnStartup,
            string appDataFolder)
        {
            InitializeComponent();

            _appDataFolder = appDataFolder;
            ActiveDeviceId = activeDeviceId;
            LaunchOnStartup = launchOnStartup;

            UpdatedSettings = new AppSettings
            {
                IdleFadeSeconds = currentSettings.IdleFadeSeconds,
                KeystrokeDisplaySeconds = currentSettings.KeystrokeDisplaySeconds,
                PrivacyHotkey = currentSettings.PrivacyHotkey,
                SpotlightHotkey = currentSettings.SpotlightHotkey,
                MarkerHotkey = currentSettings.MarkerHotkey,
                Status1Hotkey = currentSettings.Status1Hotkey,
                Status2Hotkey = currentSettings.Status2Hotkey,
                Status3Hotkey = currentSettings.Status3Hotkey
            };

            WorkingMice = savedMice.ToDictionary(
                kvp => kvp.Key,
                kvp => new MouseProfile { Name = kvp.Value.Name, AccentHex = kvp.Value.AccentHex },
                StringComparer.OrdinalIgnoreCase);

            IdleDelaySlider.Value = UpdatedSettings.IdleFadeSeconds;
            KeyDurationSlider.Value = UpdatedSettings.KeystrokeDisplaySeconds;
            IdleDelayLabel.Text = $"{UpdatedSettings.IdleFadeSeconds:0.0}s";
            KeyDurationLabel.Text = $"{UpdatedSettings.KeystrokeDisplaySeconds:0.0}s";
            StartupCheckBox.IsChecked = launchOnStartup;

            LoadHotkeyFields();
            RenderSavedMiceList();
            SyncWithWindowsTheme();

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox)
                {
                    CloseSmoothly(false);
                }
            };
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (ProfilesTabView == null || BehaviorTabView == null || ShortcutsTabView == null) return;

            ProfilesTabView.Visibility = TabProfilesBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            BehaviorTabView.Visibility = TabBehaviorBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ShortcutsTabView.Visibility = TabShortcutsBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Smooth fade + gentle 6px upward settle on tab switch
            TabContentContainer.Opacity = 0;
            TabSlideTransform.Y = 6;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var slideUp = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            TabContentContainer.BeginAnimation(OpacityProperty, fadeIn);
            TabSlideTransform.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private void LoadHotkeyFields()
        {
            PrivacyHotkeyBox.Text = UpdatedSettings.PrivacyHotkey;
            SpotlightHotkeyBox.Text = UpdatedSettings.SpotlightHotkey;
            MarkerHotkeyBox.Text = UpdatedSettings.MarkerHotkey;
            Status1HotkeyBox.Text = UpdatedSettings.Status1Hotkey;
            Status2HotkeyBox.Text = UpdatedSettings.Status2Hotkey;
            Status3HotkeyBox.Text = UpdatedSettings.Status3Hotkey;
        }

        private void RenderSavedMiceList()
        {
            MiceListPanel.Children.Clear();
            MouseCountLabel.Text = WorkingMice.Count == 1 ? "1 saved" : $"{WorkingMice.Count} saved";

            if (WorkingMice.Count == 0)
            {
                EmptyMiceState.Visibility = Visibility.Visible;
                return;
            }

            EmptyMiceState.Visibility = Visibility.Collapsed;

            var entries = WorkingMice.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                string deviceId = entries[i].Key;
                MouseProfile profile = entries[i].Value;
                bool isActive = string.Equals(deviceId, ActiveDeviceId, StringComparison.OrdinalIgnoreCase);

                var row = new Grid { Margin = new Thickness(4, 8, 4, 8) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                Color dotColor;
                try { dotColor = (Color)ColorConverter.ConvertFromString(profile.AccentHex); }
                catch { dotColor = (Color)ColorConverter.ConvertFromString("#60A5FA"); }

                infoStack.Children.Add(new Ellipse
                {
                    Width = 11,
                    Height = 11,
                    Fill = new SolidColorBrush(dotColor),
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var nameRow = new StackPanel { Orientation = Orientation.Horizontal };

                var nameText = new TextBlock
                {
                    Text = profile.Name,
                    FontSize = 13.5,
                    FontWeight = FontWeights.SemiBold
                };
                nameText.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryTextBrush");
                nameRow.Children.Add(nameText);

                if (isActive)
                {
                    nameRow.Children.Add(new Border
                    {
                        Margin = new Thickness(8, 0, 0, 0),
                        Padding = new Thickness(7, 1.5, 7, 1.5),
                        CornerRadius = new CornerRadius(6),
                        Background = new SolidColorBrush(Color.FromArgb(42, dotColor.R, dotColor.G, dotColor.B)),
                        Child = new TextBlock
                        {
                            Text = "Active",
                            FontSize = 10.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(dotColor)
                        }
                    });
                }

                var idText = new TextBlock
                {
                    Text = deviceId,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                idText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");

                textStack.Children.Add(nameRow);
                textStack.Children.Add(idText);
                infoStack.Children.Add(textStack);

                var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                if (!isActive)
                {
                    var useBtn = new Button
                    {
                        Content = "Activate",
                        Style = (Style)Resources["SubtleButton"],
                        Margin = new Thickness(0, 0, 8, 0),
                        Padding = new Thickness(11, 5, 11, 5)
                    };
                    useBtn.Click += (s, e) =>
                    {
                        ActiveDeviceId = deviceId;
                        RenderSavedMiceList();
                    };
                    actions.Children.Add(useBtn);
                }

                var editBtn = new Button
                {
                    Content = "Edit",
                    Style = (Style)Resources["SubtleButton"],
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(11, 5, 11, 5)
                };
                editBtn.Click += (s, e) =>
                {
                    var prompt = new NamePromptDialog("Edit mouse profile", profile.Name, initialAccentHex: profile.AccentHex);
                    if (prompt.ShowDialog() == true)
                    {
                        WorkingMice[deviceId] = new MouseProfile
                        {
                            Name = prompt.EnteredName,
                            AccentHex = prompt.SelectedAccentHex
                        };
                        RenderSavedMiceList();
                    }
                };
                actions.Children.Add(editBtn);

                var removeBtn = new Button
                {
                    Content = "✕",
                    Style = (Style)Resources["DangerMiniButton"],
                    Padding = new Thickness(9, 5, 9, 5),
                    ToolTip = "Delete mouse profile"
                };
                removeBtn.Click += (s, e) =>
                {
                    WorkingMice.Remove(deviceId);
                    if (string.Equals(ActiveDeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        ActiveDeviceId = WorkingMice.Keys.LastOrDefault() ?? "";
                    }
                    RenderSavedMiceList();
                };
                actions.Children.Add(removeBtn);

                Grid.SetColumn(infoStack, 0);
                Grid.SetColumn(actions, 1);
                row.Children.Add(infoStack);
                row.Children.Add(actions);
                MiceListPanel.Children.Add(row);

                if (i < entries.Count - 1)
                {
                    var sep = new Border { Height = 1, Margin = new Thickness(4, 2, 4, 2) };
                    sep.SetResourceReference(Border.BackgroundProperty, "DividerBrush");
                    MiceListPanel.Children.Add(sep);
                }
            }
        }

        private void IdleDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IdleDelayLabel != null)
            {
                IdleDelayLabel.Text = $"{e.NewValue:0.0}s";
            }
        }

        private void KeyDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (KeyDurationLabel != null)
            {
                KeyDurationLabel.Text = $"{e.NewValue:0.0}s";
            }
        }

        private void HotkeyBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox box)
            {
                _previousHotkeyText = box.Text;
                box.Text = "Press keys...";
            }
        }

        private void HotkeyBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox box && box.Text == "Press keys...")
            {
                box.Text = _previousHotkeyText;
            }
        }

        private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (sender is not TextBox box) return;

            Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            if (key == Key.Escape)
            {
                box.Text = _previousHotkeyText;
                Keyboard.ClearFocus();
                return;
            }

            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            var mods = Keyboard.Modifiers;
            var parts = new List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

            string keyName = key switch
            {
                >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (key - Key.NumPad0))).ToString(),
                _ => key.ToString()
            };

            parts.Add(keyName);
            _previousHotkeyText = string.Join("+", parts);
            box.Text = _previousHotkeyText;
            Keyboard.ClearFocus();
        }

        private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
        {
            var defaults = new AppSettings();
            PrivacyHotkeyBox.Text = defaults.PrivacyHotkey;
            SpotlightHotkeyBox.Text = defaults.SpotlightHotkey;
            MarkerHotkeyBox.Text = defaults.MarkerHotkey;
            Status1HotkeyBox.Text = defaults.Status1Hotkey;
            Status2HotkeyBox.Text = defaults.Status2Hotkey;
            Status3HotkeyBox.Text = defaults.Status3Hotkey;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_appDataFolder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = _appDataFolder,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            UpdatedSettings.IdleFadeSeconds = Math.Round(IdleDelaySlider.Value, 1);
            UpdatedSettings.KeystrokeDisplaySeconds = Math.Round(KeyDurationSlider.Value, 1);
            UpdatedSettings.PrivacyHotkey = PrivacyHotkeyBox.Text;
            UpdatedSettings.SpotlightHotkey = SpotlightHotkeyBox.Text;
            UpdatedSettings.MarkerHotkey = MarkerHotkeyBox.Text;
            UpdatedSettings.Status1Hotkey = Status1HotkeyBox.Text;
            UpdatedSettings.Status2Hotkey = Status2HotkeyBox.Text;
            UpdatedSettings.Status3Hotkey = Status3HotkeyBox.Text;
            UpdatedSettings.Sanitize();

            LaunchOnStartup = StartupCheckBox.IsChecked == true;
            CloseSmoothly(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSmoothly(false);
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
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
                Resources["CardBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7F7FA"));
                Resources["CardBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4DC"));
                Resources["SectionBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                Resources["SectionBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4EC"));
                Resources["DividerBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECF2"));
                Resources["TabBarBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEAF0"));
                Resources["ActiveTabBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                Resources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                Resources["SecondaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
                Resources["BtnBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECF2"));
                Resources["BtnHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E8"));
                Resources["BtnBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D0DA"));
                Resources["CloseHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4EA"));
                Resources["CloseIconBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
                Resources["DangerHoverBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
                CardShadow.Opacity = 0.22;
            }
        }

        private void CloseSmoothly(bool result)
        {
            if (_isClosing) return;
            _isClosing = true;

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
            var scaleOut = new DoubleAnimation(1.0, 0.96, TimeSpan.FromMilliseconds(150));

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
