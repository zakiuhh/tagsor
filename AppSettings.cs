using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace Tagsor
{
    public class AppSettings
    {
        public double IdleFadeSeconds { get; set; } = 2.5;
        public double KeystrokeDisplaySeconds { get; set; } = 1.1;

        public string PrivacyHotkey { get; set; } = "Ctrl+Alt+H";
        public string SpotlightHotkey { get; set; } = "Ctrl+Alt+S";
        public string MarkerHotkey { get; set; } = "Ctrl+Alt+D";
        public string Status1Hotkey { get; set; } = "Ctrl+Alt+1";
        public string Status2Hotkey { get; set; } = "Ctrl+Alt+2";
        public string Status3Hotkey { get; set; } = "Ctrl+Alt+3";

        public static AppSettings LoadOrCreate(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                    if (loaded != null)
                    {
                        loaded.Sanitize();
                        return loaded;
                    }
                }
            }
            catch { }

            var defaults = new AppSettings();
            defaults.Save(filePath);
            return defaults;
        }

        public void Save(string filePath)
        {
            try
            {
                Sanitize();
                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public void Sanitize()
        {
            IdleFadeSeconds = Math.Clamp(IdleFadeSeconds, 0.3, 60.0);
            KeystrokeDisplaySeconds = Math.Clamp(KeystrokeDisplaySeconds, 0.3, 15.0);

            if (string.IsNullOrWhiteSpace(PrivacyHotkey)) PrivacyHotkey = "Ctrl+Alt+H";
            if (string.IsNullOrWhiteSpace(SpotlightHotkey)) SpotlightHotkey = "Ctrl+Alt+S";
            if (string.IsNullOrWhiteSpace(MarkerHotkey)) MarkerHotkey = "Ctrl+Alt+D";
            if (string.IsNullOrWhiteSpace(Status1Hotkey)) Status1Hotkey = "Ctrl+Alt+1";
            if (string.IsNullOrWhiteSpace(Status2Hotkey)) Status2Hotkey = "Ctrl+Alt+2";
            if (string.IsNullOrWhiteSpace(Status3Hotkey)) Status3Hotkey = "Ctrl+Alt+3";
        }

        public static bool TryParseHotkey(string? shortcut, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;
            if (string.IsNullOrWhiteSpace(shortcut)) return false;

            string[] parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return false;

            Key mainKey = Key.None;

            foreach (string rawPart in parts)
            {
                string part = rawPart.ToUpperInvariant();
                switch (part)
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= 0x0002; // MOD_CONTROL
                        break;
                    case "ALT":
                        modifiers |= 0x0001; // MOD_ALT
                        break;
                    case "SHIFT":
                        modifiers |= 0x0004; // MOD_SHIFT
                        break;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= 0x0008; // MOD_WIN
                        break;
                    default:
                        if (part.Length == 1 && part[0] >= '0' && part[0] <= '9')
                        {
                            mainKey = Key.D0 + (part[0] - '0');
                        }
                        else if (part.Length == 1 && part[0] >= 'A' && part[0] <= 'Z')
                        {
                            mainKey = Key.A + (part[0] - 'A');
                        }
                        else if (Enum.TryParse<Key>(rawPart, true, out var parsedKey))
                        {
                            mainKey = parsedKey;
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
