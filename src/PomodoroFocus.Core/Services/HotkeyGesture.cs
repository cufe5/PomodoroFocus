using System;
using System.Collections.Generic;

namespace PomodoroFocus.Core.Services
{
    [Flags]
    public enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    public sealed class HotkeyGesture
    {
        public HotkeyModifiers Modifiers { get; private set; }
        public int VirtualKey { get; private set; }
        public string KeyName { get; private set; }

        public HotkeyGesture(HotkeyModifiers modifiers, int virtualKey, string keyName)
        {
            Modifiers = modifiers;
            VirtualKey = virtualKey;
            KeyName = keyName;
        }

        public string ToDisplayString()
        {
            var parts = new List<string>();
            if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
            parts.Add(KeyName);
            return string.Join("+", parts.ToArray());
        }

        public string[] ToKeyCaps()
        {
            var parts = new List<string>();
            if ((Modifiers & HotkeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & HotkeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((Modifiers & HotkeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & HotkeyModifiers.Win) != 0) parts.Add("Win");
            parts.Add(KeyName);
            return parts.ToArray();
        }

        public static bool TryParse(string text, out HotkeyGesture gesture)
        {
            gesture = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] tokens = text.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2) return false;

            HotkeyModifiers mods = HotkeyModifiers.None;
            int vk = 0;
            string keyName = null;

            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                string lower = t.ToLowerInvariant();
                if (lower == "ctrl" || lower == "control")
                {
                    mods |= HotkeyModifiers.Control;
                }
                else if (lower == "alt")
                {
                    mods |= HotkeyModifiers.Alt;
                }
                else if (lower == "shift")
                {
                    mods |= HotkeyModifiers.Shift;
                }
                else if (lower == "win" || lower == "windows")
                {
                    mods |= HotkeyModifiers.Win;
                }
                else
                {
                    if (vk != 0) return false; // multiple main keys
                    if (!TryParseKeyName(t, out vk, out keyName)) return false;
                }
            }

            if (mods == HotkeyModifiers.None || vk == 0 || string.IsNullOrEmpty(keyName))
            {
                return false;
            }

            gesture = new HotkeyGesture(mods, vk, keyName);
            return true;
        }

        public static bool TryParseKeyName(string token, out int vk, out string canonicalName)
        {
            vk = 0;
            canonicalName = null;
            if (string.IsNullOrWhiteSpace(token)) return false;

            string u = token.Trim().ToUpperInvariant();
            if (u.Length == 1)
            {
                char c = u[0];
                if (c >= 'A' && c <= 'Z')
                {
                    vk = (int)c;
                    canonicalName = u;
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    vk = (int)c;
                    canonicalName = u;
                    return true;
                }
            }

            if (u.StartsWith("F") && u.Length >= 2 && u.Length <= 3)
            {
                int fnum;
                if (int.TryParse(u.Substring(1), out fnum) && fnum >= 1 && fnum <= 12)
                {
                    vk = 0x70 + (fnum - 1);
                    canonicalName = "F" + fnum;
                    return true;
                }
            }

            switch (u)
            {
                case "SPACE": vk = 0x20; canonicalName = "Space"; return true;
                case "LEFT": vk = 0x25; canonicalName = "Left"; return true;
                case "UP": vk = 0x26; canonicalName = "Up"; return true;
                case "RIGHT": vk = 0x27; canonicalName = "Right"; return true;
                case "DOWN": vk = 0x28; canonicalName = "Down"; return true;
                case "ENTER":
                case "RETURN": vk = 0x0D; canonicalName = "Enter"; return true;
                case "TAB": vk = 0x09; canonicalName = "Tab"; return true;
                case "BACK":
                case "BACKSPACE": vk = 0x08; canonicalName = "Backspace"; return true;
                case "DELETE":
                case "DEL": vk = 0x2E; canonicalName = "Delete"; return true;
                case "HOME": vk = 0x24; canonicalName = "Home"; return true;
                case "END": vk = 0x23; canonicalName = "End"; return true;
                case "PAGEUP":
                case "PRIOR": vk = 0x21; canonicalName = "PageUp"; return true;
                case "PAGEDOWN":
                case "NEXT": vk = 0x22; canonicalName = "PageDown"; return true;
            }

            return false;
        }

        public static bool TryFormatFromVirtualKey(HotkeyModifiers modifiers, int vk, out HotkeyGesture gesture)
        {
            gesture = null;
            if (modifiers == HotkeyModifiers.None) return false;

            string name = null;
            if (vk >= 'A' && vk <= 'Z') name = ((char)vk).ToString();
            else if (vk >= '0' && vk <= '9') name = ((char)vk).ToString();
            else if (vk >= 0x70 && vk <= 0x7B) name = "F" + (vk - 0x70 + 1);
            else if (vk == 0x20) name = "Space";
            else if (vk == 0x25) name = "Left";
            else if (vk == 0x26) name = "Up";
            else if (vk == 0x27) name = "Right";
            else if (vk == 0x28) name = "Down";
            else if (vk == 0x0D) name = "Enter";
            else if (vk == 0x09) name = "Tab";
            else if (vk == 0x08) name = "Backspace";
            else if (vk == 0x2E) name = "Delete";
            else if (vk == 0x24) name = "Home";
            else if (vk == 0x23) name = "End";
            else if (vk == 0x21) name = "PageUp";
            else if (vk == 0x22) name = "PageDown";

            if (name == null) return false;
            gesture = new HotkeyGesture(modifiers, vk, name);
            return true;
        }
    }
}
