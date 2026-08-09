using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Repilot.Models;

/// <summary>Modifier keys that can accompany a main key in a chord.</summary>
[System.Flags]
public enum KeyMods
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>
/// A keyboard chord: zero or more modifiers plus an optional main virtual key.
/// Used both for "custom key combination" actions and as the backing for many
/// catalog Windows functions. Plain serializable data (no INotifyPropertyChanged) —
/// the owning <see cref="CopilotActionData"/> carries it.
/// </summary>
public sealed class KeyCombo
{
    /// <summary>Bitmask of <see cref="KeyMods"/>.</summary>
    public int Modifiers { get; set; }

    /// <summary>Virtual-key code of the main (non-modifier) key, or 0 if none.</summary>
    public int VirtualKey { get; set; }

    [JsonIgnore]
    public KeyMods Mods => (KeyMods)Modifiers;

    [JsonIgnore]
    public bool IsEmpty => Modifiers == 0 && VirtualKey == 0;

    public KeyCombo() { }

    public KeyCombo(KeyMods mods, int virtualKey)
    {
        Modifiers = (int)mods;
        VirtualKey = virtualKey;
    }

    /// <summary>Human-readable chord, e.g. "Ctrl + Shift + Esc".</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        var m = Mods;
        if (m.HasFlag(KeyMods.Win)) parts.Add("Win");
        if (m.HasFlag(KeyMods.Control)) parts.Add("Ctrl");
        if (m.HasFlag(KeyMods.Alt)) parts.Add("Alt");
        if (m.HasFlag(KeyMods.Shift)) parts.Add("Shift");
        if (VirtualKey != 0) parts.Add(KeyNames.Name(VirtualKey));
        return parts.Count == 0 ? "(none)" : string.Join(" + ", parts);
    }
}

/// <summary>Maps virtual-key codes to friendly display names.</summary>
public static class KeyNames
{
    // Common virtual-key codes (winuser.h). Modifiers are handled separately.
    public const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_ESCAPE = 0x1B,
        VK_SPACE = 0x20, VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24,
        VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28, VK_SNAPSHOT = 0x2C,
        VK_INSERT = 0x2D, VK_DELETE = 0x2E, VK_APPS = 0x5D,
        VK_F23 = 0x86;

    private static readonly Dictionary<int, string> Map = new()
    {
        [VK_BACK] = "Backspace", [VK_TAB] = "Tab", [VK_RETURN] = "Enter", [VK_ESCAPE] = "Esc",
        [VK_SPACE] = "Space", [VK_PRIOR] = "Page Up", [VK_NEXT] = "Page Down", [VK_END] = "End",
        [VK_HOME] = "Home", [VK_LEFT] = "Left", [VK_UP] = "Up", [VK_RIGHT] = "Right",
        [VK_DOWN] = "Down", [VK_SNAPSHOT] = "PrtScn", [VK_INSERT] = "Insert", [VK_DELETE] = "Delete",
        [VK_APPS] = "Menu",
        [0x2E] = "Delete", [0xBA] = ";", [0xBB] = "+", [0xBC] = ",", [0xBD] = "-", [0xBE] = ".",
        [0xBF] = "/", [0xC0] = "`", [0xDB] = "[", [0xDC] = "\\", [0xDD] = "]", [0xDE] = "'",
    };

    public static string Name(int vk)
    {
        if (Map.TryGetValue(vk, out var name)) return name;
        // A–Z and 0–9 map directly to their ASCII character
        if ((vk >= 0x30 && vk <= 0x39) || (vk >= 0x41 && vk <= 0x5A))
            return ((char)vk).ToString();
        // Numpad 0–9
        if (vk >= 0x60 && vk <= 0x69) return "Num " + (vk - 0x60);
        // Function keys F1–F24
        if (vk >= 0x70 && vk <= 0x87) return "F" + (vk - 0x70 + 1);
        return $"0x{vk:X2}";
    }
}
