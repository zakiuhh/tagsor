using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Tagsor
{
    public partial class TrayMenuPopup : Window
    {
        public event Action? OnEditMouseRequested;
        public event Action? OnResetMiceRequested;
        public event Action? OnTogglePrivacyRequested;
        public event Action? OnTogglePauseRequested;
        public event Action? OnExitRequested;

        private bool _closed = false;

        public TrayMenuPopup(string activeUserName, string accentHex, bool isPrivacyMode, bool isPaused)
        {
            InitializeComponent();

            ActiveUserBadge.Text = activeUserName;
            PrivacyCheck.Visibility = isPrivacyMode ? Visibility.Visible : Visibility.Collapsed;
            PauseCheck.Visibility = isPaused ? Visibility.Visible : Visibility.Collapsed;
            StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isPaused ? "#FBBF24" : accentHex));
            StatusText.Text = isPaused ? "Paused" : "Active";

            SyncWithWindowsTheme();

            Deactivated += (s, e) => SafeClose();
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) SafeClose();
            };
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

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            SafeClose();
            OnExitRequested?.Invoke();
        }
    }
}
