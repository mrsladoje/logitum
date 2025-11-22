namespace Loupedeck.AdaptiveRingPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using Loupedeck;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Loupedeck.AdaptiveRingPlugin.Models;
    using Loupedeck.AdaptiveRingPlugin.Models.ActionData;
    using System.Text.Json;

    /// <summary>
    /// Service to execute keybind simulations on Windows
    /// </summary>
    public static class KeybindExecutor
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int KEYEVENTF_KEYUP = 0x0002;

        // Virtual Key Codes (simplified map)
        private static readonly Dictionary<string, byte> _keyMap = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            { "Ctrl", 0x11 }, { "Control", 0x11 },
            { "Shift", 0x10 },
            { "Alt", 0x12 },
            { "Win", 0x5B }, { "Windows", 0x5B }, { "Cmd", 0x5B },
            { "Enter", 0x0D }, { "Return", 0x0D },
            { "Esc", 0x1B }, { "Escape", 0x1B },
            { "Space", 0x20 },
            { "Tab", 0x09 },
            { "Back", 0x08 }, { "Backspace", 0x08 },
            { "Del", 0x2E }, { "Delete", 0x2E },
            { "Up", 0x26 }, { "Down", 0x28 }, { "Left", 0x25 }, { "Right", 0x27 },
            { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
            { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
            { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
            { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 },
            { "E", 0x45 }, { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 },
            { "I", 0x49 }, { "J", 0x4A }, { "K", 0x4B }, { "L", 0x4C },
            { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F }, { "P", 0x50 },
            { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
            { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 },
            { "Y", 0x59 }, { "Z", 0x5A },
            { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 },
            { "4", 0x34 }, { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 },
            { "8", 0x38 }, { "9", 0x39 }
        };

        public static void Execute(AppAction action)
        {
            if (action.Type != ActionType.Keybind) return;

            try
            {
                var keybindData = JsonSerializer.Deserialize<KeybindActionData>(action.ActionDataJson);
                if (keybindData == null || keybindData.Keys == null || keybindData.Keys.Count == 0)
                {
                    PluginLog.Warning("KeybindExecutor: No keys found in action data");
                    return;
                }

                PluginLog.Info($"KeybindExecutor: Pressing {string.Join("+", keybindData.Keys)}");

                // Parse keys to bytes
                var keyCodes = new List<byte>();
                foreach (var key in keybindData.Keys)
                {
                    if (_keyMap.TryGetValue(key, out var code))
                    {
                        keyCodes.Add(code);
                    }
                    else
                    {
                        // Attempt single char parse if not found in map
                        if (key.Length == 1)
                        {
                            // This is a rough fallback, better to rely on map
                             // char c = char.ToUpper(key[0]);
                             // keyCodes.Add((byte)c); 
                             PluginLog.Warning($"KeybindExecutor: Unknown key '{key}'");
                        }
                    }
                }

                if (keyCodes.Count == 0) return;

                // Press down all keys
                foreach (var code in keyCodes)
                {
                    keybd_event(code, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                }

                // Small delay to register
                Thread.Sleep(50);

                // Release all keys in reverse order
                keyCodes.Reverse();
                foreach (var code in keyCodes)
                {
                    keybd_event(code, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }

                PluginLog.Info("KeybindExecutor: Execution complete");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"KeybindExecutor: Error executing keybind: {ex.Message}");
            }
        }
    }
}

