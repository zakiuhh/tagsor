using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using IOPath = System.IO.Path;

namespace Tagsor
{
    public partial class MainWindow : Window
    {
        private const int WM_INPUT = 0x00FF;
        private const int WM_HOTKEY = 0x0312;

        // Global Hotkey IDs
        private const int HOTKEY_PRIVACY_ID = 9001;
        private const int HOTKEY_SPOTLIGHT_ID = 9002;
        private const int HOTKEY_STATUS_1_ID = 9003;
        private const int HOTKEY_STATUS_2_ID = 9004;
        private const int HOTKEY_STATUS_3_ID = 9005;
        private const int HOTKEY_MARKER_ID = 9006;

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint VK_H = 0x48;
        private const uint VK_S = 0x53;
        private const uint VK_D = 0x44;
        private const uint VK_1 = 0x31;
        private const uint VK_2 = 0x32;
        private const uint VK_3 = 0x33;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private const string AppName = "Tagsor";
        private const uint MAPVK_VK_TO_CHAR = 2;

        private readonly string _saveFilePath;
        private Dictionary<string, MouseProfile> _savedMice = new();
        private readonly HashSet<string> _promptingDevices = new();
        private readonly HashSet<string> _skippedDevices = new();
        private CursorOverlay _overlay = null!;
        private string _activeDeviceId = "";
        private MouseProfile _activeProfile = new();

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
        private bool _isLightTheme = false;
        private Forms.NotifyIcon _trayIcon = null!;
        private TrayMenuPopup? _activeTrayMenu;

        // Keystrokes & Repeat Counter State
        private readonly List<KeyDisplayItem> _typedKeys = new();
        private DateTime _lastKeyTime = DateTime.MinValue;
        private bool _isShowingKeys = false;

        // Drag-Hold & Double-Click State
        private string? _heldMouseButton = null;
        private readonly HashSet<string> _pressedButtons = new();
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int VK_MBUTTON = 0x04;
        private DateTime _mouseDownTime = DateTime.MinValue;
        private bool _holdBadgeShown = false;
        private DateTime _lastLeftClickTime = DateTime.MinValue;

        // Spotlight Dimmer Overlay State
        private Window? _spotlightWindow;
        private RadialGradientBrush? _spotlightBrush;
        private bool _isSpotlightActive = false;

        // Live Screen Marker, Smooth Splines & Hold-to-Snap Shape State (Ctrl + Alt + D)
        private Window? _markerWindow;
        private Canvas? _markerCanvas;
        private System.Windows.Shapes.Path? _currentStrokePath;
        private readonly List<System.Windows.Point> _rawStrokePoints = new();
        private DispatcherTimer? _shapeHoldTimer;
        private bool _shapeSnappedDuringStroke = false;
        private bool _isMarkerActive = false;
        private bool _isHighlighterMode = false;
        private double _markerStrokeWidth = 4.0;
        private string _markerColorHex = "#60A5FA";
        private bool _markerAutoFade = true;

        // Quick-Switch Status Chip State
        private string? _activeStatusText = null;
        private DateTime _statusExpiryTime = DateTime.MinValue;

        // Shake-to-Locate Velocity Tracking
        private int _lastMoveDirection = 0;
        private int _directionFlipCount = 0;
        private DateTime _shakeWindowStart = DateTime.MinValue;

        private const double BaseOffsetX = -6;
        private const double BaseOffsetY = 6;

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

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

            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataFolder = IOPath.Combine(roamingAppData, "Tagsor");
            Directory.CreateDirectory(appDataFolder);
            _saveFilePath = IOPath.Combine(appDataFolder, "saved_mice.json");
            MigrateLegacySaveFile(IOPath.Combine(roamingAppData, "DualCursorApp"));

            LoadSavedMice();
            _markerColorHex = _activeProfile.AccentHex;
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
                _shapeHoldTimer?.Stop();
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                var hwnd = new WindowInteropHelper(_overlay).Handle;
                UnregisterHotKey(hwnd, HOTKEY_PRIVACY_ID);
                UnregisterHotKey(hwnd, HOTKEY_SPOTLIGHT_ID);
                UnregisterHotKey(hwnd, HOTKEY_STATUS_1_ID);
                UnregisterHotKey(hwnd, HOTKEY_STATUS_2_ID);
                UnregisterHotKey(hwnd, HOTKEY_STATUS_3_ID);
                UnregisterHotKey(hwnd, HOTKEY_MARKER_ID);
                _spotlightWindow?.Close();
                _markerWindow?.Close();
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

            _activeTrayMenu = new TrayMenuPopup(
                _activeProfile.Name,
                _activeProfile.AccentHex,
                _isPrivacyMode,
                _isOverlayPaused,
                _isSpotlightActive,
                _isMarkerActive);

            _activeTrayMenu.OnEditMouseRequested += EditActiveMouse;
            _activeTrayMenu.OnResetMiceRequested += ResetSavedMice;
            _activeTrayMenu.OnToggleSpotlightRequested += () => { if (!_isOverlayPaused) ToggleSpotlight(); };
            _activeTrayMenu.OnToggleMarkerRequested += () => { if (!_isOverlayPaused) ToggleMarkerMode(); };
            _activeTrayMenu.OnStatusChipRequested += (label, hex) => { if (!_isOverlayPaused) TriggerStatusChip(label, hex); };
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
                _markerColorHex = _activeProfile.AccentHex;

                if (!string.IsNullOrEmpty(_activeDeviceId))
                {
                    _savedMice[_activeDeviceId] = _activeProfile;
                    SaveMiceToDisk();
                }

                _activeStatusText = null;
                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
            }
        }

        private void ResetSavedMice()
        {
            _savedMice.Clear();
            SaveMiceToDisk();
            _skippedDevices.Clear();
            _activeDeviceId = "";
            _activeStatusText = null;
            _activeProfile = new MouseProfile();
            _markerColorHex = _activeProfile.AccentHex;
            _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
        }

        private void TogglePrivacyMode()
        {
            _isPrivacyMode = !_isPrivacyMode;

            if (_isPrivacyMode && _isShowingKeys)
            {
                _isShowingKeys = false;
                _typedKeys.Clear();
                RestoreNameOrStatus();
            }
        }

        private void TogglePauseOverlay()
        {
            _isOverlayPaused = !_isOverlayPaused;
            _trayIcon.Text = _isOverlayPaused ? "Tagsor (Paused)" : "Tagsor";

            if (_isOverlayPaused)
            {
                _overlay.Hide();
                if (_isSpotlightActive) ToggleSpotlight();
                if (_isMarkerActive) ToggleMarkerMode();
            }
            else
            {
                _overlay.Show();
            }
        }

        // Spotlight Dimmer Toggle (Ctrl + Alt + S)
        private void ToggleSpotlight()
        {
            _isSpotlightActive = !_isSpotlightActive;

            if (_isSpotlightActive)
            {
                if (_spotlightWindow == null)
                {
                    _spotlightBrush = new RadialGradientBrush
                    {
                        MappingMode = BrushMappingMode.Absolute,
                        RadiusX = 150,
                        RadiusY = 150
                    };
                    _spotlightBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), 0.0));
                    _spotlightBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, 0, 0, 0), 0.72));
                    _spotlightBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(145, 8, 8, 12), 1.0));

                    _spotlightWindow = new Window
                    {
                        WindowStyle = WindowStyle.None,
                        AllowsTransparency = true,
                        Background = System.Windows.Media.Brushes.Transparent,
                        Topmost = true,
                        ShowInTaskbar = false,
                        IsHitTestVisible = false,
                        Left = SystemParameters.VirtualScreenLeft,
                        Top = SystemParameters.VirtualScreenTop,
                        Width = SystemParameters.VirtualScreenWidth,
                        Height = SystemParameters.VirtualScreenHeight,
                        Content = new System.Windows.Shapes.Rectangle { Fill = _spotlightBrush }
                    };

                    _spotlightWindow.SourceInitialized += (s, e) =>
                    {
                        var hwnd = new WindowInteropHelper(_spotlightWindow).Handle;
                        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
                    };
                }

                _spotlightWindow.Opacity = 0;
                _spotlightWindow.Show();
                _spotlightWindow.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(220)));
                _overlay.EnforceTopmost();
            }
            else if (_spotlightWindow != null)
            {
                var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180));
                fadeOut.Completed += (s, e) => _spotlightWindow?.Hide();
                _spotlightWindow.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        // Live Screen Marker + Smooth Catmull-Rom Splines + Hold-to-Snap Shape Recognition (Ctrl + Alt + D)
        private void ToggleMarkerMode()
        {
            _isMarkerActive = !_isMarkerActive;

            if (_isMarkerActive)
            {
                _markerWindow?.Close();
                _markerColorHex = _activeProfile.AccentHex;

                _shapeHoldTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(520)
                };
                _shapeHoldTimer.Tick += (s, e) =>
                {
                    _shapeHoldTimer.Stop();
                    if (_currentStrokePath != null && !_shapeSnappedDuringStroke && _rawStrokePoints.Count >= 6)
                    {
                        TrySnapCurrentStrokeToShape();
                    }
                };

                var rootGrid = new Grid();

                _markerCanvas = new Canvas
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)),
                    Cursor = System.Windows.Input.Cursors.Pen
                };

                _markerCanvas.MouseLeftButtonDown += (s, e) =>
                {
                    _rawStrokePoints.Clear();
                    _shapeSnappedDuringStroke = false;

                    var startPt = e.GetPosition(_markerCanvas);
                    _rawStrokePoints.Add(startPt);

                    var baseColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_markerColorHex);
                    var strokeColor = _isHighlighterMode
                        ? System.Windows.Media.Color.FromArgb(105, baseColor.R, baseColor.G, baseColor.B)
                        : baseColor;

                    _currentStrokePath = new System.Windows.Shapes.Path
                    {
                        Stroke = new SolidColorBrush(strokeColor),
                        StrokeThickness = _isHighlighterMode ? _markerStrokeWidth * 3.4 : _markerStrokeWidth,
                        StrokeStartLineCap = _isHighlighterMode ? PenLineCap.Flat : PenLineCap.Round,
                        StrokeEndLineCap = _isHighlighterMode ? PenLineCap.Flat : PenLineCap.Round,
                        StrokeLineJoin = PenLineJoin.Round,
                        Data = BuildSmoothSplineGeometry(_rawStrokePoints),
                        Effect = _isHighlighterMode ? null : new DropShadowEffect
                        {
                            Color = baseColor,
                            BlurRadius = 10,
                            ShadowDepth = 0,
                            Opacity = 0.85
                        }
                    };

                    _markerCanvas.Children.Add(_currentStrokePath);
                    _markerCanvas.CaptureMouse();
                    _shapeHoldTimer?.Stop();
                    _shapeHoldTimer?.Start();
                };

                _markerCanvas.MouseMove += (s, e) =>
                {
                    if (_currentStrokePath != null && e.LeftButton == MouseButtonState.Pressed)
                    {
                        var pt = e.GetPosition(_markerCanvas);
                        var last = _rawStrokePoints[^1];
                        double dist = Distance(pt, last);

                        if (dist >= 2.5)
                        {
                            if (_shapeSnappedDuringStroke)
                            {
                                _rawStrokePoints[^1] = pt;
                                TrySnapCurrentStrokeToShape(isDynamicResize: true);
                            }
                            else
                            {
                                _rawStrokePoints.Add(pt);
                                _currentStrokePath.Data = BuildSmoothSplineGeometry(_rawStrokePoints);

                                _shapeHoldTimer?.Stop();
                                _shapeHoldTimer?.Start();
                            }
                        }
                    }
                };

                _markerCanvas.MouseLeftButtonUp += (s, e) =>
                {
                    _shapeHoldTimer?.Stop();

                    if (_currentStrokePath != null)
                    {
                        var finishedPath = _currentStrokePath;
                        _currentStrokePath = null;
                        _markerCanvas.ReleaseMouseCapture();

                        if (_markerAutoFade)
                        {
                            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(650))
                            {
                                BeginTime = TimeSpan.FromSeconds(2.4)
                            };
                            fadeOut.Completed += (_, _) => _markerCanvas?.Children.Remove(finishedPath);
                            finishedPath.BeginAnimation(OpacityProperty, fadeOut);
                        }
                    }
                };

                rootGrid.Children.Add(_markerCanvas);
                rootGrid.Children.Add(BuildMarkerSidebar());

                _markerWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Topmost = true,
                    ShowInTaskbar = false,
                    Left = SystemParameters.VirtualScreenLeft,
                    Top = SystemParameters.VirtualScreenTop,
                    Width = SystemParameters.VirtualScreenWidth,
                    Height = SystemParameters.VirtualScreenHeight,
                    Content = rootGrid
                };

                _markerWindow.Show();
                _markerWindow.Activate();
                _overlay.EnforceTopmost();

                TriggerStatusChip(_isHighlighterMode ? "🖍 Highlighter" : "✏️ Pen mode", _markerColorHex);
            }
            else if (_markerWindow != null)
            {
                _shapeHoldTimer?.Stop();
                _currentStrokePath = null;
                _markerWindow.Close();
                _markerWindow = null;
                _markerCanvas = null;

                if (_activeStatusText != null && (_activeStatusText.StartsWith("✏️") || _activeStatusText.StartsWith("🖍") || _activeStatusText.StartsWith("✨")))
                {
                    _activeStatusText = null;
                    _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
                }
            }
        }

        // --- Smooth Catmull-Rom to Cubic Bézier Spline Builder ---
        private static PathGeometry BuildSmoothSplineGeometry(IReadOnlyList<System.Windows.Point> points)
        {
            var geometry = new PathGeometry();
            if (points.Count == 0) return geometry;

            var figure = new PathFigure { StartPoint = points[0], IsClosed = false };

            if (points.Count == 1)
            {
                figure.Segments.Add(new LineSegment(new System.Windows.Point(points[0].X + 0.5, points[0].Y + 0.5), true));
                geometry.Figures.Add(figure);
                return geometry;
            }

            if (points.Count == 2)
            {
                figure.Segments.Add(new LineSegment(points[1], true));
                geometry.Figures.Add(figure);
                return geometry;
            }

            var pts = new List<System.Windows.Point>(points.Count) { points[0] };
            for (int i = 1; i < points.Count - 1; i++)
            {
                pts.Add(new System.Windows.Point(
                    0.2 * points[i - 1].X + 0.6 * points[i].X + 0.2 * points[i + 1].X,
                    0.2 * points[i - 1].Y + 0.6 * points[i].Y + 0.2 * points[i + 1].Y));
            }
            pts.Add(points[^1]);

            var bezierPoints = new PointCollection();
            const double tension = 0.5;

            for (int i = 0; i < pts.Count - 1; i++)
            {
                var p0 = i == 0 ? pts[0] : pts[i - 1];
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p3 = i + 2 < pts.Count ? pts[i + 2] : pts[^1];

                var cp1 = new System.Windows.Point(
                    p1.X + (p2.X - p0.X) * tension / 3.0,
                    p1.Y + (p2.Y - p0.Y) * tension / 3.0);

                var cp2 = new System.Windows.Point(
                    p2.X - (p3.X - p1.X) * tension / 3.0,
                    p2.Y - (p3.Y - p1.Y) * tension / 3.0);

                bezierPoints.Add(cp1);
                bezierPoints.Add(cp2);
                bezierPoints.Add(p2);
            }

            figure.Segments.Add(new PolyBezierSegment(bezierPoints, true));
            geometry.Figures.Add(figure);
            return geometry;
        }

        // --- Hold-to-Snap Geometric Shape Recognizer ---
        private void TrySnapCurrentStrokeToShape(bool isDynamicResize = false)
        {
            if (_currentStrokePath == null || _rawStrokePoints.Count < 6) return;

            var pts = _rawStrokePoints;
            double minX = pts.Min(p => p.X);
            double maxX = pts.Max(p => p.X);
            double minY = pts.Min(p => p.Y);
            double maxY = pts.Max(p => p.Y);
            double width = maxX - minX;
            double height = maxY - minY;
            double diag = Hypot(width, height);

            if (diag < 24) return;

            double totalLength = 0;
            for (int i = 1; i < pts.Count; i++)
                totalLength += Distance(pts[i - 1], pts[i]);

            double startEndDist = Distance(pts[0], pts[^1]);
            bool isClosed = startEndDist < Math.Max(36, diag * 0.24) && totalLength > diag * 1.4;

            Geometry? snappedGeometry = null;
            string detectedName = "";

            if (!isClosed)
            {
                var simplifiedOpen = DouglasPeucker(pts, diag * 0.08);
                if (simplifiedOpen.Count == 3)
                {
                    var a = simplifiedOpen[0];
                    var b = simplifiedOpen[1];
                    var c = simplifiedOpen[2];
                    double leg1 = Distance(a, b);
                    double leg2 = Distance(b, c);
                    double angle = AngleBetween(a, b, c);

                    if (b.Y > a.Y && b.Y > c.Y && leg2 > leg1 * 1.15 && angle >= 35 && angle <= 115)
                    {
                        var fig = new PathFigure { StartPoint = a, IsClosed = false };
                        fig.Segments.Add(new LineSegment(b, true));
                        fig.Segments.Add(new LineSegment(c, true));
                        var pg = new PathGeometry();
                        pg.Figures.Add(fig);
                        snappedGeometry = pg;
                        detectedName = "Checkmark";
                    }
                }

                if (snappedGeometry == null && simplifiedOpen.Count >= 3 && simplifiedOpen.Count <= 5)
                {
                    int splitIdx = (int)(pts.Count * 0.72);
                    var shaftStart = pts[0];
                    var tip = pts[splitIdx];
                    double maxTailDist = 0;
                    for (int i = splitIdx; i < pts.Count; i++)
                    {
                        double d = Distance(pts[i], tip);
                        if (d > maxTailDist) maxTailDist = d;
                    }

                    double shaftLen = Distance(shaftStart, tip);
                    if (shaftLen > 45 && maxTailDist > 12 && maxTailDist < shaftLen * 0.48 && Distance(pts[^1], tip) < shaftLen * 0.45)
                    {
                        snappedGeometry = BuildArrowGeometry(shaftStart, tip);
                        detectedName = "Arrow";
                    }
                }

                if (snappedGeometry == null)
                {
                    double maxDeviation = 0;
                    for (int i = 1; i < pts.Count - 1; i++)
                    {
                        double dev = PerpendicularDistance(pts[i], pts[0], pts[^1]);
                        if (dev > maxDeviation) maxDeviation = dev;
                    }

                    if (maxDeviation < Math.Max(14, startEndDist * 0.085))
                    {
                        var p1 = pts[0];
                        var p2 = pts[^1];
                        double angleRad = Math.Abs(Math.Atan2(p2.Y - p1.Y, p2.X - p1.X));
                        if (angleRad < 0.12 || Math.Abs(angleRad - Math.PI) < 0.12)
                            p2 = new System.Windows.Point(p2.X, p1.Y);
                        else if (Math.Abs(angleRad - Math.PI / 2.0) < 0.12)
                            p2 = new System.Windows.Point(p1.X, p2.Y);

                        snappedGeometry = new LineGeometry(p1, p2);
                        detectedName = "Line";
                    }
                }
            }
            else
            {
                var center = new System.Windows.Point((minX + maxX) / 2.0, (minY + maxY) / 2.0);
                double rx = width / 2.0;
                double ry = height / 2.0;

                double radialErrorSum = 0;
                var radii = new List<double>(pts.Count);
                foreach (var p in pts)
                {
                    double nx = (p.X - center.X) / Math.Max(1, rx);
                    double ny = (p.Y - center.Y) / Math.Max(1, ry);
                    double normDist = Math.Sqrt(nx * nx + ny * ny);
                    radialErrorSum += Math.Abs(normDist - 1.0);
                    radii.Add(Distance(p, center));
                }
                double meanRadialError = radialErrorSum / pts.Count;

                var closedPts = new List<System.Windows.Point>(pts) { pts[0] };
                var simplified = DouglasPeucker(closedPts, diag * 0.065);
                if (simplified.Count > 1 && Distance(simplified[0], simplified[^1]) < 15)
                    simplified.RemoveAt(simplified.Count - 1);

                int peaks = CountRadialPeaks(radii);

                if (peaks == 5 && simplified.Count >= 8)
                {
                    double outerR = Math.Max(rx, ry);
                    snappedGeometry = BuildStarGeometry(center, outerR, outerR * 0.42);
                    detectedName = "Star";
                }
                else if (meanRadialError < 0.135)
                {
                    double aspect = rx / Math.Max(1, ry);
                    if (aspect >= 0.84 && aspect <= 1.18)
                    {
                        double r = (rx + ry) / 2.0;
                        snappedGeometry = new EllipseGeometry(center, r, r);
                        detectedName = "Circle";
                    }
                    else
                    {
                        snappedGeometry = new EllipseGeometry(center, rx, ry);
                        detectedName = "Ellipse";
                    }
                }
                else if (simplified.Count == 3)
                {
                    var fig = new PathFigure { StartPoint = simplified[0], IsClosed = true };
                    fig.Segments.Add(new LineSegment(simplified[1], true));
                    fig.Segments.Add(new LineSegment(simplified[2], true));
                    var pg = new PathGeometry();
                    pg.Figures.Add(fig);
                    snappedGeometry = pg;
                    detectedName = "Triangle";
                }
                else if (simplified.Count == 4 || meanRadialError >= 0.135)
                {
                    double aspect = width / Math.Max(1, height);
                    if (aspect >= 0.85 && aspect <= 1.18)
                    {
                        double side = (width + height) / 2.0;
                        var sqRect = new Rect(center.X - side / 2.0, center.Y - side / 2.0, side, side);
                        snappedGeometry = new RectangleGeometry(sqRect, 6, 6);
                        detectedName = "Square";
                    }
                    else
                    {
                        snappedGeometry = new RectangleGeometry(new Rect(minX, minY, width, height), 6, 6);
                        detectedName = "Rectangle";
                    }
                }
            }

            if (snappedGeometry != null)
            {
                _shapeSnappedDuringStroke = true;
                _currentStrokePath.Data = snappedGeometry;

                if (!isDynamicResize)
                {
                    var center = new System.Windows.Point((minX + maxX) / 2.0, (minY + maxY) / 2.0);
                    var st = new ScaleTransform(1.06, 1.06, center.X, center.Y);
                    _currentStrokePath.RenderTransform = st;
                    var settle = new DoubleAnimation(1.06, 1.0, TimeSpan.FromMilliseconds(180))
                    {
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
                    };
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, settle);

                    TriggerStatusChip($"✨ {detectedName}", _markerColorHex);
                }
            }
        }

        private static PathGeometry BuildArrowGeometry(System.Windows.Point start, System.Windows.Point tip)
        {
            double angle = Math.Atan2(tip.Y - start.Y, tip.X - start.X);
            double headLen = Math.Clamp(Distance(start, tip) * 0.22, 14, 28);
            const double wingAngle = 0.48;

            var leftWing = new System.Windows.Point(
                tip.X - headLen * Math.Cos(angle - wingAngle),
                tip.Y - headLen * Math.Sin(angle - wingAngle));

            var rightWing = new System.Windows.Point(
                tip.X - headLen * Math.Cos(angle + wingAngle),
                tip.Y - headLen * Math.Sin(angle + wingAngle));

            var pg = new PathGeometry();

            var shaft = new PathFigure { StartPoint = start, IsClosed = false };
            shaft.Segments.Add(new LineSegment(tip, true));
            pg.Figures.Add(shaft);

            var head = new PathFigure { StartPoint = leftWing, IsClosed = false };
            head.Segments.Add(new LineSegment(tip, true));
            head.Segments.Add(new LineSegment(rightWing, true));
            pg.Figures.Add(head);

            return pg;
        }

        private static PathGeometry BuildStarGeometry(System.Windows.Point center, double outerR, double innerR)
        {
            var pts = new List<System.Windows.Point>(10);
            double startAngle = -Math.PI / 2.0;

            for (int i = 0; i < 10; i++)
            {
                double r = (i % 2 == 0) ? outerR : innerR;
                double a = startAngle + i * (Math.PI / 5.0);
                pts.Add(new System.Windows.Point(center.X + r * Math.Cos(a), center.Y + r * Math.Sin(a)));
            }

            var fig = new PathFigure { StartPoint = pts[0], IsClosed = true };
            for (int i = 1; i < pts.Count; i++)
                fig.Segments.Add(new LineSegment(pts[i], true));

            var pg = new PathGeometry();
            pg.Figures.Add(fig);
            return pg;
        }

        private static int CountRadialPeaks(List<double> radii)
        {
            if (radii.Count < 10) return 0;
            double avg = radii.Average();
            double minR = radii.Min();
            double maxR = radii.Max();
            if (maxR - minR < avg * 0.25) return 0;

            int peaks = 0;
            bool above = false;
            double threshold = avg * 1.08;

            foreach (double r in radii)
            {
                if (!above && r > threshold)
                {
                    above = true;
                    peaks++;
                }
                else if (above && r < avg * 0.96)
                {
                    above = false;
                }
            }
            return peaks;
        }

        private static List<System.Windows.Point> DouglasPeucker(IReadOnlyList<System.Windows.Point> points, double epsilon)
        {
            if (points.Count < 3) return new List<System.Windows.Point>(points);

            double maxDist = 0;
            int index = 0;
            for (int i = 1; i < points.Count - 1; i++)
            {
                double d = PerpendicularDistance(points[i], points[0], points[^1]);
                if (d > maxDist)
                {
                    index = i;
                    maxDist = d;
                }
            }

            if (maxDist > epsilon)
            {
                var left = DouglasPeucker(points.Take(index + 1).ToList(), epsilon);
                var right = DouglasPeucker(points.Skip(index).ToList(), epsilon);
                left.RemoveAt(left.Count - 1);
                left.AddRange(right);
                return left;
            }

            return new List<System.Windows.Point> { points[0], points[^1] };
        }

        private static double PerpendicularDistance(System.Windows.Point pt, System.Windows.Point lineStart, System.Windows.Point lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double mag = Hypot(dx, dy);
            if (mag < 1e-6) return Distance(pt, lineStart);
            return Math.Abs(dy * pt.X - dx * pt.Y + lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X) / mag;
        }

        private static double AngleBetween(System.Windows.Point a, System.Windows.Point b, System.Windows.Point c)
        {
            double v1x = a.X - b.X, v1y = a.Y - b.Y;
            double v2x = c.X - b.X, v2y = c.Y - b.Y;
            double dot = v1x * v2x + v1y * v2y;
            double mag = Hypot(v1x, v1y) * Hypot(v2x, v2y);
            if (mag < 1e-6) return 0;
            return Math.Acos(Math.Clamp(dot / mag, -1.0, 1.0)) * (180.0 / Math.PI);
        }

        private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

        private static double Distance(System.Windows.Point a, System.Windows.Point b)
        {
            return Hypot(a.X - b.X, a.Y - b.Y);
        }

        private Border BuildMarkerSidebar()
        {
            var bgBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_isLightTheme ? "#F8F8FA" : "#28282D"));
            var borderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_isLightTheme ? "#D4D4DC" : "#42424A"));
            var textBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_isLightTheme ? "#18181B" : "#F3F3F5"));
            var activePillBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_isLightTheme ? "#E4E4EC" : "#3A3A44"));
            var dividerBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(_isLightTheme ? "#E4E4EC" : "#383840"));

            double leftOffset = Math.Max(0, -SystemParameters.VirtualScreenLeft) + 16;
            double topOffset = Math.Max(0, -SystemParameters.VirtualScreenTop) + (SystemParameters.PrimaryScreenHeight / 2.0) - 210;

            var sidebarSlide = new TranslateTransform(-24, 0);
            var sidebarBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(leftOffset, Math.Max(24, topOffset), 0, 0),
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(7, 10, 7, 10),
                Cursor = System.Windows.Input.Cursors.Arrow,
                RenderTransform = sidebarSlide,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 4,
                    Opacity = _isLightTheme ? 0.22 : 0.55,
                    Color = Colors.Black
                }
            };

            sidebarBorder.Loaded += (s, e) =>
            {
                sidebarSlide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-24, 0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };

            // 1. Tool Mode: Pen vs Highlighter
            var penBtn = CreateSidebarIconButton("✏️", "Pen tool (hold stroke to snap shape)", !_isHighlighterMode, textBrush, activePillBrush);
            var highBtn = CreateSidebarIconButton("🖍", "Highlighter tool", _isHighlighterMode, textBrush, activePillBrush);

            penBtn.Click += (s, e) =>
            {
                _isHighlighterMode = false;
                penBtn.Background = activePillBrush;
                highBtn.Background = System.Windows.Media.Brushes.Transparent;
                TriggerStatusChip("✏️ Pen mode", _markerColorHex);
            };

            highBtn.Click += (s, e) =>
            {
                _isHighlighterMode = true;
                highBtn.Background = activePillBrush;
                penBtn.Background = System.Windows.Media.Brushes.Transparent;
                TriggerStatusChip("🖍 Highlighter", _markerColorHex);
            };

            stack.Children.Add(penBtn);
            stack.Children.Add(highBtn);
            stack.Children.Add(CreateSidebarDivider(dividerBrush));

            // 2. Stroke Width: Thin (3), Medium (6), Thick (11)
            var widthButtons = new List<Button>();
            double[] widths = { 3.0, 6.0, 11.0 };
            double[] dotSizes = { 5.0, 9.0, 14.0 };
            string[] widthTips = { "Thin stroke", "Medium stroke", "Thick stroke" };

            for (int i = 0; i < widths.Length; i++)
            {
                double w = widths[i];
                var dot = new Ellipse
                {
                    Width = dotSizes[i],
                    Height = dotSizes[i],
                    Fill = textBrush
                };

                var wBtn = new Button
                {
                    Width = 34,
                    Height = 32,
                    Margin = new Thickness(0, 2, 0, 2),
                    Background = Math.Abs(_markerStrokeWidth - w) < 0.5 ? activePillBrush : System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = widthTips[i],
                    Content = dot,
                    Template = CreateRoundedButtonTemplate()
                };

                wBtn.Click += (s, e) =>
                {
                    _markerStrokeWidth = w;
                    foreach (var b in widthButtons) b.Background = System.Windows.Media.Brushes.Transparent;
                    wBtn.Background = activePillBrush;
                };

                widthButtons.Add(wBtn);
                stack.Children.Add(wBtn);
            }

            stack.Children.Add(CreateSidebarDivider(dividerBrush));

            // 3. Color Swatches
            var swatchRings = new List<Border>();
            string[] swatches = { _activeProfile.AccentHex, "#F87171", "#FBBF24", "#34D399", "#FFFFFF" };
            string[] swatchNames = { "Accent color", "Coral Red", "Amber Yellow", "Emerald Green", "Pure White" };

            for (int i = 0; i < swatches.Length; i++)
            {
                string hex = swatches[i];
                var ring = new Border
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(0, 3, 0, 3),
                    CornerRadius = new CornerRadius(12),
                    BorderThickness = new Thickness(2),
                    BorderBrush = string.Equals(_markerColorHex, hex, StringComparison.OrdinalIgnoreCase)
                        ? textBrush
                        : System.Windows.Media.Brushes.Transparent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = swatchNames[i],
                    Child = new Ellipse
                    {
                        Width = 16,
                        Height = 16,
                        Fill = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)),
                        Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 128, 128, 128)),
                        StrokeThickness = 1
                    }
                };

                ring.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    _markerColorHex = hex;
                    foreach (var r in swatchRings) r.BorderBrush = System.Windows.Media.Brushes.Transparent;
                    ring.BorderBrush = textBrush;
                };

                swatchRings.Add(ring);
                stack.Children.Add(ring);
            }

            stack.Children.Add(CreateSidebarDivider(dividerBrush));

            // 4. Pin Strokes (Disable Auto-Fade), Clear Canvas, Exit Draw Mode
            var pinBtn = CreateSidebarIconButton("📌", "Keep strokes pinned (disable 2.5s auto-fade)", !_markerAutoFade, textBrush, activePillBrush);
            pinBtn.Click += (s, e) =>
            {
                _markerAutoFade = !_markerAutoFade;
                pinBtn.Background = !_markerAutoFade ? activePillBrush : System.Windows.Media.Brushes.Transparent;
                TriggerStatusChip(_markerAutoFade ? "⏳ Auto-fade on" : "📌 Strokes pinned", _markerColorHex);
            };

            var clearBtn = CreateSidebarIconButton("🗑", "Clear all strokes", false, textBrush, activePillBrush);
            clearBtn.Click += (s, e) => _markerCanvas?.Children.Clear();

            var closeBtn = CreateSidebarIconButton("✕", "Exit Draw Mode (Esc)", false,
                new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FB7185")),
                activePillBrush);
            closeBtn.Click += (s, e) => ToggleMarkerMode();

            stack.Children.Add(pinBtn);
            stack.Children.Add(clearBtn);
            stack.Children.Add(closeBtn);

            sidebarBorder.Child = stack;
            return sidebarBorder;
        }

        private static Button CreateSidebarIconButton(string glyph, string tooltip, bool isSelected, System.Windows.Media.Brush fg, System.Windows.Media.Brush activeBg)
        {
            return new Button
            {
                Width = 34,
                Height = 32,
                Margin = new Thickness(0, 2, 0, 2),
                Background = isSelected ? activeBg : System.Windows.Media.Brushes.Transparent,
                Foreground = fg,
                FontSize = 14,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = tooltip,
                Content = new TextBlock
                {
                    Text = glyph,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Template = CreateRoundedButtonTemplate()
            };
        }

        private static Border CreateSidebarDivider(System.Windows.Media.Brush dividerBrush)
        {
            return new Border
            {
                Height = 1,
                Margin = new Thickness(4, 6, 4, 6),
                Background = dividerBrush
            };
        }

        private static ControlTemplate CreateRoundedButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;
            return template;
        }

        // Quick-Switch Status Chip (Ctrl + Alt + 1 / 2 / 3)
        private void TriggerStatusChip(string statusLabel, string hexColor)
        {
            if (_activeStatusText == statusLabel)
            {
                _activeStatusText = null;
                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
                return;
            }

            _isShowingKeys = false;
            _typedKeys.Clear();
            _activeStatusText = statusLabel;
            _statusExpiryTime = DateTime.UtcNow.AddSeconds(5);
            _lastActivityTime = DateTime.UtcNow;

            _overlay.SetIdleState(false);
            _overlay.ShowStatusChip(statusLabel, hexColor);
        }

        private void RestoreNameOrStatus()
        {
            if (!string.IsNullOrEmpty(_activeStatusText) && DateTime.UtcNow < _statusExpiryTime)
            {
                string color = _activeStatusText.StartsWith("👀") ? "#38BDF8"
                             : _activeStatusText.StartsWith("⏸") ? "#F59E0B"
                             : _activeStatusText.StartsWith("✅") ? "#34D399"
                             : _markerColorHex;
                _overlay.ShowStatusChip(_activeStatusText, color);
            }
            else
            {
                _activeStatusText = null;
                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
            }
        }

        private void OnOverlaySourceInitialized(object? sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(_overlay).Handle;
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            RegisterHotKey(hwnd, HOTKEY_PRIVACY_ID, MOD_CONTROL | MOD_ALT, VK_H);
            RegisterHotKey(hwnd, HOTKEY_SPOTLIGHT_ID, MOD_CONTROL | MOD_ALT, VK_S);
            RegisterHotKey(hwnd, HOTKEY_STATUS_1_ID, MOD_CONTROL | MOD_ALT, VK_1);
            RegisterHotKey(hwnd, HOTKEY_STATUS_2_ID, MOD_CONTROL | MOD_ALT, VK_2);
            RegisterHotKey(hwnd, HOTKEY_STATUS_3_ID, MOD_CONTROL | MOD_ALT, VK_3);
            RegisterHotKey(hwnd, HOTKEY_MARKER_ID, MOD_CONTROL | MOD_ALT, VK_D);

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

                if (_isSpotlightActive && _spotlightBrush != null)
                {
                    var center = new System.Windows.Point(
                        cursorDipX - SystemParameters.VirtualScreenLeft,
                        cursorDipY - SystemParameters.VirtualScreenTop);
                    _spotlightBrush.Center = center;
                    _spotlightBrush.GradientOrigin = center;
                }

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

            SyncMouseButtonsFromAsyncState();

            if (_heldMouseButton != null && !_holdBadgeShown && (DateTime.UtcNow - _mouseDownTime).TotalMilliseconds >= 220)
            {
                _holdBadgeShown = true;
                _overlay.SetHoldIndicator(_heldMouseButton);
            }

            if (_isShowingKeys && (DateTime.UtcNow - _lastKeyTime).TotalSeconds >= 1.1)
            {
                _isShowingKeys = false;
                _typedKeys.Clear();
                RestoreNameOrStatus();
            }

            if (!_isShowingKeys && !string.IsNullOrEmpty(_activeStatusText) && DateTime.UtcNow >= _statusExpiryTime)
            {
                _activeStatusText = null;
                _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
            }

            bool isIdle = !_isSpotlightActive && !_isMarkerActive && _heldMouseButton == null && (DateTime.UtcNow - _lastActivityTime).TotalSeconds > 2.5;
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
                    _isLightTheme = (useLight == 1);
                    _overlay.ApplyTheme(_isLightTheme);
                }
            }
            catch
            {
                _isLightTheme = false;
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
            if (msg == WM_HOTKEY)
            {
                switch (wParam.ToInt32())
                {
                    case HOTKEY_PRIVACY_ID:
                        TogglePrivacyMode();
                        handled = true;
                        break;
                    case HOTKEY_SPOTLIGHT_ID:
                        if (!_isOverlayPaused) ToggleSpotlight();
                        handled = true;
                        break;
                    case HOTKEY_MARKER_ID:
                        if (!_isOverlayPaused) ToggleMarkerMode();
                        handled = true;
                        break;
                    case HOTKEY_STATUS_1_ID:
                        if (!_isOverlayPaused) TriggerStatusChip("👀 Look here", "#38BDF8");
                        handled = true;
                        break;
                    case HOTKEY_STATUS_2_ID:
                        if (!_isOverlayPaused) TriggerStatusChip("⏸ Hold on", "#F59E0B");
                        handled = true;
                        break;
                    case HOTKEY_STATUS_3_ID:
                        if (!_isOverlayPaused) TriggerStatusChip("✅ Done", "#34D399");
                        handled = true;
                        break;
                }
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
                int vk = keyboardData.Keyboard.VirutalKey;

                if (!keyboardData.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up) && vk == 0x1B)
                {
                    if (_isMarkerActive)
                    {
                        ToggleMarkerMode();
                    }
                    if (!string.IsNullOrEmpty(_activeStatusText))
                    {
                        _activeStatusText = null;
                        _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex, forceShowName: true);
                    }
                    if (_isSpotlightActive)
                    {
                        ToggleSpotlight();
                    }
                }

                if (_isPrivacyMode) return;

                if (!keyboardData.Keyboard.Flags.HasFlag(RawKeyboardFlags.Up) && vk > 0 && vk < 255)
                {
                    if (IsPasswordFieldFocused()) return;

                    var keyItem = CreateKeyDisplayItem(vk);
                    if (keyItem != null)
                    {
                        _lastActivityTime = DateTime.UtcNow;
                        _lastKeyTime = DateTime.UtcNow;
                        _isShowingKeys = true;

                        if (_typedKeys.Count > 0 && _typedKeys[^1].KeyId == keyItem.KeyId)
                        {
                            _typedKeys[^1].RepeatCount++;
                        }
                        else
                        {
                            if (_typedKeys.Count >= 3)
                            {
                                _typedKeys.Clear();
                            }
                            _typedKeys.Add(keyItem);
                        }

                        _overlay.ShowTypedKeys(_typedKeys);
                    }
                }
                return;
            }

            if (data is RawInputMouseData mouseData)
            {
                _lastActivityTime = DateTime.UtcNow;

                int dx = mouseData.Mouse.LastX;
                _overlay.SetTiltFromVelocity(dx);

                if (Math.Abs(dx) >= 18)
                {
                    int dir = Math.Sign(dx);
                    if (dir != _lastMoveDirection)
                    {
                        if ((DateTime.UtcNow - _shakeWindowStart).TotalMilliseconds > 420)
                        {
                            _directionFlipCount = 0;
                            _shakeWindowStart = DateTime.UtcNow;
                        }

                        _lastMoveDirection = dir;
                        _directionFlipCount++;

                        if (_directionFlipCount >= 4)
                        {
                            _directionFlipCount = 0;
                            _overlay.TriggerShakeSurge();
                        }
                    }
                }

                // A single raw report can carry several button flags (e.g. a fast click reports
                // down and up together), so handle every flag instead of an else-if chain.
                // Button handling must not depend on the device lookup below: synthesized input
                // (touchpads, remote desktop, mouse-sharing tools, some drivers) has no device handle.
                var buttons = mouseData.Mouse.Buttons;
                if (buttons.HasFlag(RawMouseButtonFlags.LeftButtonDown)) OnMouseButtonChanged("LMB", true);
                if (buttons.HasFlag(RawMouseButtonFlags.RightButtonDown)) OnMouseButtonChanged("RMB", true);
                if (buttons.HasFlag(RawMouseButtonFlags.MiddleButtonDown)) OnMouseButtonChanged("MMB", true);
                if (buttons.HasFlag(RawMouseButtonFlags.LeftButtonUp)) OnMouseButtonChanged("LMB", false);
                if (buttons.HasFlag(RawMouseButtonFlags.RightButtonUp)) OnMouseButtonChanged("RMB", false);
                if (buttons.HasFlag(RawMouseButtonFlags.MiddleButtonUp)) OnMouseButtonChanged("MMB", false);

                if (buttons.HasFlag(RawMouseButtonFlags.MouseWheel))
                {
                    _overlay.TriggerScrollBounce(mouseData.Mouse.ButtonData);
                }

                // Per-mouse profiles need a real device; input without one keeps the current profile.
                RawInputDevice? sourceDevice = null;
                try { sourceDevice = mouseData.Device; } catch { }
                if (sourceDevice == null) return;

                string deviceId = sourceDevice.DevicePath ?? mouseData.Header.DeviceHandle.ToString();
                _activeDeviceId = deviceId;

                if (_savedMice.TryGetValue(deviceId, out MouseProfile? savedProfile))
                {
                    if (_activeProfile.Name != savedProfile.Name || _activeProfile.AccentHex != savedProfile.AccentHex)
                    {
                        _activeProfile = savedProfile;
                        _markerColorHex = savedProfile.AccentHex;
                        if (!_isShowingKeys && string.IsNullOrEmpty(_activeStatusText))
                        {
                            _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex);
                        }
                    }
                }
                else if (!_promptingDevices.Contains(deviceId) && !_skippedDevices.Contains(deviceId))
                {
                    // Unknown mouse: always ask for a name. On a fresh install (nothing saved yet)
                    // this is the first-run welcome prompt.
                    bool isFirstRun = _savedMice.Count == 0;
                    _promptingDevices.Add(deviceId);
                    Dispatcher.BeginInvoke(() => PromptForNewMouseName(deviceId, isFirstRun));
                }
            }
        }

        private void OnMouseButtonChanged(string label, bool isDown)
        {
            var now = DateTime.UtcNow;

            if (isDown)
            {
                if (!_pressedButtons.Add(label)) return; // already tracked as held

                _lastActivityTime = now;

                bool isDoubleClick = false;
                if (label == "LMB")
                {
                    isDoubleClick = (now - _lastLeftClickTime).TotalMilliseconds <= 350;
                    _lastLeftClickTime = now;
                }

                _heldMouseButton = label;
                _mouseDownTime = now;
                _holdBadgeShown = false;
                _overlay.TriggerClickAnimation(true, label, isDoubleClick);
                return;
            }

            if (!_pressedButtons.Remove(label)) return;
            _lastActivityTime = now;

            if (_pressedButtons.Count > 0)
            {
                // Another button is still held: keep showing that one.
                _heldMouseButton = _pressedButtons.First();
                _mouseDownTime = now;
                _holdBadgeShown = false;
                _overlay.SetHoldIndicator(null);
                _overlay.TriggerClickAnimation(true, _heldMouseButton);
            }
            else
            {
                _heldMouseButton = null;
                _holdBadgeShown = false;
                _overlay.TriggerClickAnimation(false);
            }
        }

        // Safety net: raw input can miss button events (injected input, UI-privilege boundaries,
        // driver quirks). Reconcile with the real button state; OnMouseButtonChanged is idempotent.
        private void SyncMouseButtonsFromAsyncState()
        {
            ReconcileButton("LMB", VK_LBUTTON);
            ReconcileButton("RMB", VK_RBUTTON);
            ReconcileButton("MMB", VK_MBUTTON);
        }

        private void ReconcileButton(string label, int virtualKey)
        {
            bool isDown = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
            if (isDown != _pressedButtons.Contains(label))
            {
                OnMouseButtonChanged(label, isDown);
            }
        }

        private KeyDisplayItem? CreateKeyDisplayItem(int vkCode)
        {
            Key wpfKey = KeyInterop.KeyFromVirtualKey(vkCode);
            if (wpfKey == Key.None) return null;

            string keyId = wpfKey.ToString();

            switch (wpfKey)
            {
                case Key.Return:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 19,5 V 11 A 4,4 0 0 1 15,15 H 5 M 9,11 L 5,15 L 9,19" };
                case Key.Space:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 3,13 V 16 A 1.5,1.5 0 0 0 4.5,17.5 H 19.5 A 1.5,1.5 0 0 0 21,16 V 13" };
                case Key.LeftShift:
                case Key.RightShift:
                    return new KeyDisplayItem { KeyId = "Shift", IconGeometry = "M 12,3 L 21,12 H 16 V 20 A 1,1 0 0 1 15,21 H 9 A 1,1 0 0 1 8,20 V 12 H 3 Z" };
                case Key.LeftCtrl:
                case Key.RightCtrl:
                    return new KeyDisplayItem { KeyId = "Ctrl", IconGeometry = "M 5,15 L 12,8 L 19,15" };
                case Key.LeftAlt:
                case Key.RightAlt:
                    return new KeyDisplayItem { KeyId = "Alt", IconGeometry = "M 3,6 H 8.5 L 15.5,18 H 21 M 15,6 H 21" };
                case Key.LWin:
                case Key.RWin:
                    return new KeyDisplayItem { KeyId = "Win", IconGeometry = "M 3,3 H 10.5 V 10.5 H 3 Z M 13.5,3 H 21 V 10.5 H 13.5 Z M 3,13.5 H 10.5 V 21 H 3 Z M 13.5,13.5 H 21 V 21 H 13.5 Z" };
                case Key.Back:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 9,4 H 20 A 2,2 0 0 1 22,6 V 18 A 2,2 0 0 1 20,20 H 9 L 2,12 Z M 12,9.5 L 17,14.5 M 17,9.5 L 12,14.5" };
                case Key.Tab:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 3,12 H 16 M 11,7 L 16,12 L 11,17 M 20,5 V 19" };
                case Key.Capital:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 12,3 L 20,11 H 15.5 V 16 H 8.5 V 11 H 4 Z M 8,20 H 16" };
                case Key.Up:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 12,20 V 4 M 6,10 L 12,4 L 18,10" };
                case Key.Down:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 12,4 V 20 M 6,14 L 12,20 L 18,14" };
                case Key.Left:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 20,12 H 4 M 10,6 L 4,12 L 10,18" };
                case Key.Right:
                    return new KeyDisplayItem { KeyId = keyId, IconGeometry = "M 4,12 H 20 M 14,6 L 20,12 L 14,18" };

                case Key.Escape: return new KeyDisplayItem { KeyId = keyId, Label = "Esc" };
                case Key.Delete: return new KeyDisplayItem { KeyId = keyId, Label = "Del" };
                case Key.Insert: return new KeyDisplayItem { KeyId = keyId, Label = "Ins" };
                case Key.Home: return new KeyDisplayItem { KeyId = keyId, Label = "Home" };
                case Key.End: return new KeyDisplayItem { KeyId = keyId, Label = "End" };
                case Key.PageUp: return new KeyDisplayItem { KeyId = keyId, Label = "PgUp" };
                case Key.PageDown: return new KeyDisplayItem { KeyId = keyId, Label = "PgDn" };
                case Key.PrintScreen: return new KeyDisplayItem { KeyId = keyId, Label = "PrtSc" };
                case Key.NumLock: return new KeyDisplayItem { KeyId = keyId, Label = "Num" };
                case Key.Scroll: return new KeyDisplayItem { KeyId = keyId, Label = "ScrLk" };
                case Key.Pause: return new KeyDisplayItem { KeyId = keyId, Label = "Pause" };
                case Key.Apps: return new KeyDisplayItem { KeyId = keyId, Label = "Menu" };
                case Key.VolumeMute: return new KeyDisplayItem { KeyId = keyId, Label = "Mute" };
                case Key.VolumeUp: return new KeyDisplayItem { KeyId = keyId, Label = "Vol+" };
                case Key.VolumeDown: return new KeyDisplayItem { KeyId = keyId, Label = "Vol-" };
                case Key.MediaPlayPause: return new KeyDisplayItem { KeyId = keyId, Label = "Play" };
                case Key.MediaNextTrack: return new KeyDisplayItem { KeyId = keyId, Label = "Next" };
                case Key.MediaPreviousTrack: return new KeyDisplayItem { KeyId = keyId, Label = "Prev" };

                case >= Key.D0 and <= Key.D9:
                    string dLabel = ((char)('0' + (wpfKey - Key.D0))).ToString();
                    return new KeyDisplayItem { KeyId = dLabel, Label = dLabel };
                case >= Key.NumPad0 and <= Key.NumPad9:
                    string nLabel = ((char)('0' + (wpfKey - Key.NumPad0))).ToString();
                    return new KeyDisplayItem { KeyId = nLabel, Label = nLabel };
                case Key.Multiply: return new KeyDisplayItem { KeyId = "*", Label = "*" };
                case Key.Add: return new KeyDisplayItem { KeyId = "+", Label = "+" };
                case Key.Subtract: return new KeyDisplayItem { KeyId = "-", Label = "-" };
                case Key.Decimal: return new KeyDisplayItem { KeyId = ".", Label = "." };
                case Key.Divide: return new KeyDisplayItem { KeyId = "/", Label = "/" };
                case >= Key.F1 and <= Key.F12:
                    return new KeyDisplayItem { KeyId = keyId, Label = keyId };
            }

            uint mappedChar = MapVirtualKey((uint)vkCode, MAPVK_VK_TO_CHAR) & 0xFFFF;
            if (mappedChar > 0 && !char.IsControl((char)mappedChar))
            {
                string ch = char.ToUpperInvariant((char)mappedChar).ToString();
                return new KeyDisplayItem { KeyId = ch, Label = ch };
            }

            string fallback = wpfKey.ToString();
            if (fallback.StartsWith("Oem", StringComparison.OrdinalIgnoreCase)) return null;

            return new KeyDisplayItem { KeyId = fallback, Label = fallback };
        }

        private void PromptForNewMouseName(string deviceId, bool isFirstRun)
        {
            var dialog = isFirstRun
                ? new NamePromptDialog("Welcome to Tagsor", Environment.UserName, "What should we call you? This name is shown next to your cursor.")
                : new NamePromptDialog();

            if (dialog.ShowDialog() == true)
            {
                _activeProfile = new MouseProfile
                {
                    Name = dialog.EnteredName,
                    AccentHex = dialog.SelectedAccentHex
                };
                _markerColorHex = _activeProfile.AccentHex;
                _savedMice[deviceId] = _activeProfile;
                SaveMiceToDisk();

                if (!_isShowingKeys && string.IsNullOrEmpty(_activeStatusText))
                {
                    _overlay.SetUserNameAnimated(_activeProfile.Name, _activeProfile.AccentHex);
                }
            }
            else
            {
                // Dismissed: don't nag on every mouse movement. Asked again on next launch,
                // or any time via tray menu > Rename mouse.
                _skippedDevices.Add(deviceId);
            }
            _promptingDevices.Remove(deviceId);
        }

        // Older versions stored profiles in %AppData%\DualCursorApp. Move them to %AppData%\Tagsor once,
        // so existing users keep their saved names instead of being asked again.
        private void MigrateLegacySaveFile(string legacyFolder)
        {
            try
            {
                string legacyFile = IOPath.Combine(legacyFolder, "saved_mice.json");
                if (File.Exists(_saveFilePath) || !File.Exists(legacyFile)) return;

                File.Move(legacyFile, _saveFilePath);

                if (!Directory.EnumerateFileSystemEntries(legacyFolder).Any())
                {
                    Directory.Delete(legacyFolder);
                }
            }
            catch
            {
                // Best effort: if the move fails the app simply asks for names again.
            }
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
