using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Tagsor
{
    public partial class NamePromptDialog : Window
    {
        public string EnteredName { get; private set; } = "Guest";
        public string SelectedAccentHex { get; private set; } = "#60A5FA";
        private bool _isClosing = false;

        public NamePromptDialog(string? promptTitle = null, string? initialName = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(promptTitle)) HeaderLabel.Text = promptTitle;
            if (!string.IsNullOrEmpty(initialName)) NameInput.Text = initialName;

            SyncWithWindowsTheme();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            Loaded += (s, e) =>
            {
                NameInput.Focus();
                NameInput.SelectAll();
            };

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CloseSmoothly(false);
                }
            };

            Closed += (s, e) => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                Dispatcher.Invoke(SyncWithWindowsTheme);
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
                Resources["CardBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8F8FA"));
                Resources["CardBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4DC"));
                Resources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#18181B"));
                Resources["SecondaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
                Resources["InputBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
                Resources["InputBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4DC"));
                Resources["BtnBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECF0"));
                Resources["BtnHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E2E8"));
                Resources["BtnBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8C8D2"));
                Resources["CloseHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E4E4EA"));
                Resources["CloseIconBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
                CardShadow.Opacity = 0.25;
            }
            else
            {
                Resources["CardBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28282D"));
                Resources["CardBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42424A"));
                Resources["PrimaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F3F5"));
                Resources["SecondaryTextBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1A1AA"));
                Resources["InputBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202024"));
                Resources["InputBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3C44"));
                Resources["BtnBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#36363E"));
                Resources["BtnHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#44444E"));
                Resources["BtnBorderBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A4A54"));
                Resources["CloseHoverBgBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3A3A42"));
                Resources["CloseIconBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
                CardShadow.Opacity = 0.55;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSmoothly(false);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NameInput.Text))
            {
                EnteredName = NameInput.Text.Trim();
            }

            if (ColorBlue.IsChecked == true) SelectedAccentHex = "#60A5FA";
            else if (ColorGreen.IsChecked == true) SelectedAccentHex = "#4ADE80";
            else if (ColorAmber.IsChecked == true) SelectedAccentHex = "#FBBF24";
            else if (ColorPurple.IsChecked == true) SelectedAccentHex = "#C084FC";
            else if (ColorRose.IsChecked == true) SelectedAccentHex = "#FB7185";

            CloseSmoothly(true);
        }

        private void CloseSmoothly(bool result)
        {
            if (_isClosing) return;
            _isClosing = true;

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
            var scaleOut = new DoubleAnimation(1.0, 0.94, TimeSpan.FromMilliseconds(150));

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
