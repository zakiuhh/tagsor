using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Tagsor
{
    public partial class MainWindow : Window
    {
        private const int WM_INPUT = 0x00FF;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_PRIVACY_ID = 9001;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_H = 0x48;

        private const string AppName = "Tagsor";
        private const uint MAPVK_VK_TO_CHAR = 2;

        private readonly string _saveFilePath;
        private Dictionary<string, MouseProfile> _savedMice = new();
        private readonly HashSet<string> _promptingDevices = new();
        private CursorOverlay _overlay = null!;
        private string _activeDeviceId = "";
        private MouseProfile _activeProfile = new() { Name = "Zaki", AccentHex = "#60A5FA" };

        private DispatcherTimer _trackingTimer = null!;
        private int _topmostTickCounter = 0;
        private DateTime _lastActivityTime = DateTime.UtcNow;
        private POINT _lastCursorPos;

        private double _currentOverlayX;
        private double _currentOverlayY;
        private bool _hasInitializedPosition = false;
        private const double SmoothFactor = 0.38;

        private bool _isOverlayPaused = false;
        private bool _isPrivacyMode = false;
        private Forms.NotifyIcon _trayIcon = null!;
        private TrayMenuPopup? _activeTrayMenu;

        private readonly List<KeyDisplayItem> _typedKeys = new();
        private DateTime _lastKeyTime = DateTime.MinValue;
        private bool _isShowingKeys = false;

        private const double BaseOffsetX = 4;
        private const double BaseOffsetY = 12;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        public MainWindow()
        {
            InitializeComponent();

            this.Width = 0;
            this.Height = 0;
            this.WindowStyle = WindowStyle.None;
            this.ShowInTaskbar = false;
            this.Visibility = Visibility.Hidden;

            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DualCursorApp");
            Directory.CreateDirectory(appDataFolder);
            _saveFilePath = Path.Combine(appDataFolder, "saved_mice.json");

            LoadSavedMice();
            RegisterWindowsStartup();
            SetupSystemTray();

            _overlay = new CursorOverlay(_activeProfile.Name);
            _overlay.SetAccentColor(_activeProfile.AccentHex);
            _overlay.SourceInitialized += OnOverlaySourceInitialized;
            _overlay.Show();
            SyncWithWindowsTheme();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            _trackingTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(8)
            };
            _trackingTimer.Tick += OnTrackingTick;
            _trackingTimer.Start();

            this.Closed += (s, e) =>
            {
                _trackingTimer.Stop();
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                var hwnd = new WindowInteropHelper(_overlay).Handle;
                UnregisterHotKey(hwnd, HOTKEY_PRIVACY_ID);
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _overlay.Close();
            };
        }

        private void SetupSystemTray()
        {
            System.Drawing.Icon trayIconGraphic = SystemIcons.Application;
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    trayIconGraphic = System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? SystemIcons.Application;
                }
            }
            catch { }

            _trayIcon = new Forms.NotifyIcon
            {
                Icon = trayIconGraphic,
                Text = "Tagsor",
                Visible = true
            };

            _trayIcon.MouseUp += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left || e.Button == Forms.MouseButtons.Right)
                {
                    Dispatcher.Invoke(ShowCustomTrayMenu);
                }
            };
        }

        private void ShowCustomTrayMenu()
        {
            if (_activeTrayMenu != null && _activeTrayMenu.IsVisible)
            {
                _activeTrayMenu.Close();
                _activeTrayMenu = null;
                return;
            }

            _activeTrayMenu = new TrayMenuPopup(_activeProfile.Name, _activeProfile.AccentHex, _isPrivacyMode, _isOverlayPaused);
            _activeTrayMenu.OnEditMouseRequested += EditActiveMouse;
            _activeTrayMenu.OnResetMiceRequested += ResetSavedMice;
            _activeTrayMenu.OnTogglePrivacyRequested += TogglePrivacyMode;
            _activeTrayMenu.OnTogglePauseRequested += TogglePauseOverlay;
            _activeTrayMenu.OnExitRequested += Close;

            if (GetCursorPos(out POINT pt))
            {
                var dpi = VisualTreeHelper.GetDpi(_overlay);
                _activeTrayMenu.PositionNearCursor(pt.X / dpi.DpiScaleX, pt.Y / dpi.DpiScaleY);
            }

            _activeTrayMenu.Show();
            _activeTrayMenu.Activate();
        }

        private void EditActiveMouse()
        {
            var dialog = new NamePromptDialog("Rename mouse", _activeProfile.Name);
            if (dialog.ShowDialog() == true)
            {
                _activeProfile = new MouseProfile
                {
                    Name = dialog.EnteredName,
                    AccentHex = dialog.SelectedAccentHex
                };

                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    _savedMice[_activeDeviceId] = _activeProfile;
                    SaveMiceToDisk();
                }

                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
            }
        }

        private void ResetSavedMice()
        {
            _savedMice.Clear();
            SaveMiceToDisk();
            _activeDeviceId = "";
            _activeProfile = new MouseProfile { Name = "Zaki", AccentHex = "#60A5FA" };
            _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
        }

        private void TogglePrivacyMode()
        {
            _isPrivacyMode = !_isPrivacyMode;

            if (_isPrivacyMode && _isShowingKeys)
            {
                _isShowingKeys = false;
                _typedKeys.Clear();
                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
            }
        }

        private void TogglePauseOverlay()
        {
            _isOverlayPaused = !_isOverlayPaused;
            _trayIcon.Text = _isOverlayPaused ? "Tagsor (Paused)" : "Tagsor";

            if (_isOverlayPaused)
                _overlay.Hide();
            else
                _overlay.Show();
        }

        private void OnOverlaySourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(_overlay).Handle;
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            RegisterHotKey(hwnd, HOTKEY_PRIVACY_ID, MOD_CONTROL | MOD_ALT, VK_H);

            RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse, RawInputDeviceFlags.InputSink, hwnd);
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, hwnd);
        }

        private void OnTrackingTick(object? sender, EventArgs e)
        {
            if (_isOverlayPaused) return;

            if (GetCursorPos(out POINT pt))
            {
                if (pt.X != _lastCursorPos.X || pt.Y != _lastCursorPos.Y)
                {
                    _lastCursorPos = pt;
                    _lastActivityTime = DateTime.UtcNow;
                }

                var dpi = VisualTreeHelper.GetDpi(_overlay);
                double cursorDipX = pt.X / dpi.DpiScaleX;
                double cursorDipY = pt.Y / dpi.DpiScaleY;

                double screenW = SystemParameters.VirtualScreenWidth;
                double screenH = SystemParameters.VirtualScreenHeight;
                double pillW = _overlay.ActualWidth > 0 ? _overlay.ActualWidth : 90;
                double pillH = _overlay.ActualHeight > 0 ? _overlay.ActualHeight : 45;

                double targetX = (cursorDipX + BaseOffsetX + pillW > screenW)
                    ? cursorDipX - pillW + 4
                    : cursorDipX + BaseOffsetX;

                double targetY = (cursorDipY + BaseOffsetY + pillH > screenH)
                    ? cursorDipY - pillH + 2
                    : cursorDipY + BaseOffsetY;

                if (!_hasInitializedPosition)
                {
                    _currentOverlayX = targetX;
                    _currentOverlayY = targetY;
                    _hasInitializedPosition = true;
                }
                else
                {
                    _currentOverlayX += (targetX - _currentOverlayX) * SmoothFactor;
                    _currentOverlayY += (targetY - _currentOverlayY) * SmoothFactor;
                }

                _overlay.Left = _currentOverlayX;
                _overlay.Top = _currentOverlayY;
            }

            if (_isShowingKeys && (DateTime.UtcNow - _lastKeyTime).TotalSeconds >= 1.0)
            {
                _isShowingKeys = false;
                _typedKeys.Clear();
                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
            }

            bool isIdle = (DateTime.UtcNow - _lastActivityTime).TotalSeconds > 2.5;
            _overlay.SetIdleState(isIdle);

            if (++_topmostTickCounter >= 15)
            {
                _topmostTickCounter = 0;
                _overlay.EnforceTopmost();
            }
        }

        private bool IsPasswordFieldFocused()
        {
            try
            {
                var focused = AutomationElement.FocusedElement;
                return focused != null && focused.Current.IsPassword;
            }
            catch
            {
                return false;
            }
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
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int useLight)
                {
                    _overlay.ApplyTheme(useLight == 1);
                }
            }
            catch
            {
                _overlay.ApplyTheme(false);
            }
        }

        private void RegisterWindowsStartup()
        {
            try
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && !exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    key?.DeleteValue("DualCursorTag", false);
                    key?.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_PRIVACY_ID)
            {
                TogglePrivacyMode();
                handled = true;
            }
            else if (msg == WM_INPUT && !_isOverlayPaused)
            {
                ProcessRawInput(lParam);
            }
            return IntPtr.Zero;
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            var data = RawInputData.FromHandle(lParam);

            if (data is RawInputKeyboardData keyboardData)
            {
                if (_isPrivacyMode) return;

                int vk = keyboardData.Keyboard.VirutalKey;
                if (!keyboardData.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up) && vk > 0 && vk < 255)
                {
                    if (IsPasswordFieldFocused()) return;

                    var keyItem = CreateKeyDisplayItem(vk);
                    if (keyItem != null)
                    {
                        _lastActivityTime = DateTime.UtcNow;
                        _lastKeyTime = DateTime.UtcNow;
                        _isShowingKeys = true;

                        if (_typedKeys.Count >= 3)
                        {
                            _typedKeys.Clear();
                        }
                        _typedKeys.Add(keyItem);

                        _overlay.ShowTypedKeys(_typedKeys);
                    }
                }
                return;
            }

            if (data is RawInputMouseData mouseData && mouseData.Device != null)
            {
                _lastActivityTime = DateTime.UtcNow;

                _overlay.SetTiltFromVelocity(mouseData.Mouse.LastX);

                var buttons = mouseData.Mouse.Buttons;
                if (buttons.HasFlag(RawMouseButtonFlags.LeftButtonDown) || buttons.HasFlag(RawMouseButtonFlags.RightButtonDown))
                    _overlay.TriggerClickAnimation(true, isMiddleClick: false);
                else if (buttons.HasFlag(RawMouseButtonFlags.MiddleButtonDown))
                    _overlay.TriggerClickAnimation(true, isMiddleClick: true);
                else if (buttons.HasFlag(RawMouseButtonFlags.LeftButtonUp) || buttons.HasFlag(RawMouseButtonFlags.RightButtonUp) || buttons.HasFlag(RawMouseButtonFlags.MiddleButtonUp))
                    _overlay.TriggerClickAnimation(false);

                if (buttons.HasFlag(RawMouseButtonFlags.MouseWheel))
                {
                    _overlay.TriggerScrollBounce(mouseData.Mouse.ButtonData);
                }

                string deviceId = mouseData.Device.DevicePath ?? mouseData.Header.DeviceHandle.ToString();
                _activeDeviceId = deviceId;

                if (_savedMice.TryGetValue(deviceId, out MouseProfile? savedProfile))
                {
                    if (_activeProfile.Name != savedProfile.Name || _activeProfile.AccentHex != savedProfile.AccentHex)
                    {
                        _activeProfile = savedProfile;
                        if (!_isShowingKeys)
                        {
                            _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex);
                        }
                    }
                }
                else if (!_promptingDevices.Contains(deviceId))
                {
                    if (_savedMice.Count == 0)
                    {
                        _activeProfile = new MouseProfile { Name = "Zaki", AccentHex = "#60A5FA" };
                        _savedMice[deviceId] = _activeProfile;
                        SaveMiceToDisk();
                        if (!_isShowingKeys)
                        {
                            _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex);
                        }
                    }
                    else
                    {
                        _promptingDevices.Add(deviceId);
                        Dispatcher.BeginInvoke(() => PromptForNewMouseName(deviceId));
                    }
                }
            }
        }

        private KeyDisplayItem? CreateKeyDisplayItem(int vkCode)
        {
            Key wpfKey = KeyInterop.KeyFromVirtualKey(vkCode);
            if (wpfKey == Key.None) return null;

            switch (wpfKey)
            {
                case Key.Return:
                    return new KeyDisplayItem { IconGeometry = "M 19,5 V 11 A 4,4 0 0 1 15,15 H 5 M 9,11 L 5,15 L 9,19" };
                case Key.Space:
                    return new KeyDisplayItem { IconGeometry = "M 3,13 V 16 A 1.5,1.5 0 0 0 4.5,17.5 H 19.5 A 1.5,1.5 0 0 0 21,16 V 13" };
                case Key.LeftShift:
                case Key.RightShift:
                    return new KeyDisplayItem { IconGeometry = "M 12,3 L 21,12 H 16 V 20 A 1,1 0 0 1 15,21 H 9 A 1,1 0 0 1 8,20 V 12 H 3 Z" };
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    return new KeyDisplayItem { IconGeometry = "M 5,15 L 12,8 L 19,15" };
                case Key.LeftAlt:
                case Key.RightAlt:
                    return new KeyDisplayItem { IconGeometry = "M 3,6 H 8.5 L 15.5,18 H 21 M 15,6 H 21" };
                case Key.LWin:
                case Key.RWin:
                    return new KeyDisplayItem { IconGeometry = "M 3,3 H 10.5 V 10.5 H 3 Z M 13.5,3 H 21 V 10.5 H 13.5 Z M 3,13.5 H 10.5 V 21 H 3 Z M 13.5,13.5 H 21 V 21 H 13.5 Z" };
                case Key.Back:
                    return new KeyDisplayItem { IconGeometry = "M 9,4 H 20 A 2,2 0 0 1 22,6 V 18 A 2,2 0 0 1 20,20 H 9 L 2,12 Z M 12,9.5 L 17,14.5 M 17,9.5 L 12,14.5" };
                case Key.Tab:
                    return new KeyDisplayItem { IconGeometry = "M 3,12 H 16 M 11,7 L 16,12 L 11,17 M 20,5 V 19" };
                case Key.Capital:
                    return new KeyDisplayItem { IconGeometry = "M 12,3 L 20,11 H 15.5 V 16 H 8.5 V 11 H 4 Z M 8,20 H 16" };
                case Key.Up:
                    return new KeyDisplayItem { IconGeometry = "M 12,20 V 4 M 6,10 L 12,4 L 18,10" };
                case Key.Down:
                    return new KeyDisplayItem { IconGeometry = "M 12,4 V 20 M 6,14 L 12,20 L 18,14" };
                case Key.Left:
                    return new KeyDisplayItem { IconGeometry = "M 20,12 H 4 M 10,6 L 4,12 L 10,18" };
                case Key.Right:
                    return new KeyDisplayItem { IconGeometry = "M 4,12 H 20 M 14,6 L 20,12 L 14,18" };

                case Key.Escape: return new KeyDisplayItem { Label = "Esc" };
                case Key.Delete: return new KeyDisplayItem { Label = "Del" };
                case Key.Insert: return new KeyDisplayItem { Label = "Ins" };
                case Key.Home: return new KeyDisplayItem { Label = "Home" };
                case Key.End: return new KeyDisplayItem { Label = "End" };
                case Key.PageUp: return new KeyDisplayItem { Label = "PgUp" };
                case Key.PageDown: return new KeyDisplayItem { Label = "PgDn" };
                case Key.PrintScreen: return new KeyDisplayItem { Label = "PrtSc" };
                case Key.NumLock: return new KeyDisplayItem { Label = "Num" };
                case Key.Scroll: return new KeyDisplayItem { Label = "ScrLk" };
                case Key.Pause: return new KeyDisplayItem { Label = "Pause" };
                case Key.Apps: return new KeyDisplayItem { Label = "Menu" };
                case Key.VolumeMute: return new KeyDisplayItem { Label = "Mute" };
                case Key.VolumeUp: return new KeyDisplayItem { Label = "Vol+" };
                case Key.VolumeDown: return new KeyDisplayItem { Label = "Vol-" };
                case Key.MediaPlayPause: return new KeyDisplayItem { Label = "Play" };
                case Key.MediaNextTrack: return new KeyDisplayItem { Label = "Next" };
                case Key.MediaPreviousTrack: return new KeyDisplayItem { Label = "Prev" };

                case >= Key.D0 and <= Key.D9:
                    return new KeyDisplayItem { Label = ((char)('0' + (wpfKey - Key.D0))).ToString() };
                case >= Key.NumPad0 and <= Key.NumPad9:
                    return new KeyDisplayItem { Label = ((char)('0' + (wpfKey - Key.NumPad0))).ToString() };
                case Key.Multiply: return new KeyDisplayItem { Label = "*" };
                case Key.Add: return new KeyDisplayItem { Label = "+" };
                case Key.Subtract: return new KeyDisplayItem { Label = "-" };
                case Key.Decimal: return new KeyDisplayItem { Label = "." };
                case Key.Divide: return new KeyDisplayItem { Label = "/" };
                case >= Key.F1 and <= Key.F12:
                    return new KeyDisplayItem { Label = wpfKey.ToString() };
            }

            uint mappedChar = MapVirtualKey((uint)vkCode, MAPVK_VK_TO_CHAR) & 0xFFFF;
            if (mappedChar > 0 && !char.IsControl((char)mappedChar))
            {
                return new KeyDisplayItem { Label = char.ToUpperInvariant((char)mappedChar).ToString() };
            }

            string fallback = wpfKey.ToString();
            if (fallback.StartsWith("Oem", StringComparison.OrdinalIgnoreCase)) return null;

            return new KeyDisplayItem { Label = fallback };
        }

        private void PromptForNewMouseName(string deviceId)
        {
            var dialog = new NamePromptDialog();
            if (dialog.ShowDialog() == true)
            {
                _activeProfile = new MouseProfile
                {
                    Name = dialog.EnteredName,
                    AccentHex = dialog.SelectedAccentHex
                };
                _savedMice[deviceId] = _activeProfile;
                SaveMiceToDisk();

                if (!_isShowingKeys)
                {
                    _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex);
                }
            }
            _promptingDevices.Remove(deviceId);
        }

        private void LoadSavedMice()
        {
            if (File.Exists(_saveFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_saveFilePath);
                    _savedMice = JsonSerializer.Deserialize<Dictionary<string, MouseProfile>>(json) ?? new();
                }
                catch
                {
                    _savedMice = new();
                }
            }
        }

        private void SaveMiceToDisk()
        {
            string json = JsonSerializer.Serialize(_savedMice, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_saveFilePath, json);
        }
    }
}
