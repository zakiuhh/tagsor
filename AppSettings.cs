using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace Tagsor
{
    public class AppSettings
    {
        // Timing
        public double IdleFadeSeconds { get; set; } = 2.5;
        public double KeystrokeDisplaySeconds { get; set; } = 1.1;

        // Appearance
        public double PillScale { get; set; } = 1.0;          // 0.8 to 1.4
        public double PillCornerRadius { get; set; } = 15.0;  // 4.0 (sharp badge) to 22.0 (full pill)
        public double PillOpacity { get; set; } = 1.0;        // 0.4 to 1.0
        public string PositionOffsetMode { get; set; } = "BottomRight"; // "BottomRight", "TopRight", "CenteredBelow"

        // Shortcuts
        public string PrivacyHotkey { get; set; } = "Ctrl+Alt+H";
        public string SpotlightHotkey { get; set; } = "Ctrl+Alt+S";
        public string MarkerHotkey { get; set; } = "Ctrl+Alt+D";
        public string Status1Hotkey { get; set; } = "Ctrl+Alt+1";
        public string Status2Hotkey { get; set; } = "Ctrl+Alt+2";
        public string Status3Hotkey { get; set; } = "Ctrl+Alt+3";

        public void Sanitize()
        {
            IdleFadeSeconds = Math.Clamp(IdleFadeSeconds, 0.5, 30.0);
            KeystrokeDisplaySeconds = Math.Clamp(KeystrokeDisplaySeconds, 0.3, 10.0);

            PillScale = Math.Clamp(PillScale, 0.8, 1.4);
            PillCornerRadius = Math.Clamp(PillCornerRadius, 4.0, 22.0);
            PillOpacity = Math.Clamp(PillOpacity, 0.4, 1.0);

            if (PositionOffsetMode != "BottomRight" &&
                PositionOffsetMode != "TopRight" &&
                PositionOffsetMode != "CenteredBelow")
            {
                PositionOffsetMode = "BottomRight";
            }

            if (string.IsNullOrWhiteSpace(PrivacyHotkey)) PrivacyHotkey = "Ctrl+Alt+H";
            if (string.IsNullOrWhiteSpace(SpotlightHotkey)) SpotlightHotkey = "Ctrl+Alt+S";
            if (string.IsNullOrWhiteSpace(MarkerHotkey)) MarkerHotkey = "Ctrl+Alt+D";
            if (string.IsNullOrWhiteSpace(Status1Hotkey)) Status1Hotkey = "Ctrl+Alt+1";
            if (string.IsNullOrWhiteSpace(Status2Hotkey)) Status2Hotkey = "Ctrl+Alt+2";
            if (string.IsNullOrWhiteSpace(Status3Hotkey)) Status3Hotkey = "Ctrl+Alt+3";
        }

        public static AppSettings LoadOrCreate(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        loaded.Sanitize();
                        return loaded;
                    }
                }
                catch
                {
                    try { File.Copy(filePath, filePath + ".corrupt.bak", overwrite: true); } catch { }
                }
            }

            var defaults = new AppSettings();
            defaults.Save(filePath);
            return defaults;
        }

        public void Save(string filePath)
        {
            try
            {
                Sanitize();
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public static bool TryParseHotkey(string? shortcut, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;
            if (string.IsNullOrWhiteSpace(shortcut)) return false;

            var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Key mainKey = Key.None;

            foreach (var raw in parts)
            {
                switch (raw.ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= 0x0002;
                        break;
                    case "ALT":
                        modifiers |= 0x0001;
                        break;
                    case "SHIFT":
                        modifiers |= 0x0004;
                        break;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= 0x0008;
                        break;
                    default:
                        if (raw.Length == 1 && char.IsDigit(raw[0]))
                        {
                            mainKey = (Key)((int)Key.D0 + (raw[0] - '0'));
                        }
                        else if (Enum.TryParse<Key>(raw, true, out var parsed))
                        {
                            mainKey = parsed;
                        }
                        break;
                }
            }

            if (mainKey == Key.None) return false;
            int vk = KeyInterop.VirtualKeyFromKey(mainKey);
            if (vk <= 0) return false;

            virtualKey = (uint)vk;
            return true;
        }
    }
}
