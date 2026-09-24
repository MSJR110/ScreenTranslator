using System.Windows.Input;

namespace ScreenTranslator.Services;

/// <summary>A modifier+key chord, serialized as "Ctrl+Alt+T".</summary>
public readonly record struct Hotkey(ModifierKeys Modifiers, Key Key)
{
    public static readonly Hotkey None = new(ModifierKeys.None, Key.None);

    public bool IsEmpty => Key == Key.None;

    /// <summary>A chord needs at least one modifier (or an F-key) so it can't hijack normal typing.</summary>
    public bool IsValid => !IsEmpty && (Modifiers != ModifierKeys.None || (Key >= Key.F1 && Key <= Key.F24));

    public override string ToString()
    {
        if (IsEmpty) return "";
        var mods = ModifiersToString(Modifiers);
        return mods.Length == 0 ? KeyName(Key) : mods + "+" + KeyName(Key);
    }

    public static string ModifiersToString(ModifierKeys m)
    {
        var parts = new List<string>();
        if (m.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (m.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (m.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (m.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        return string.Join("+", parts);
    }

    public static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num" + (key - Key.NumPad0),
        Key.OemPlus => "=", Key.OemMinus => "-", Key.OemComma => ",", Key.OemPeriod => ".",
        Key.OemQuestion => "/", Key.OemTilde => "`", Key.OemOpenBrackets => "[", Key.OemCloseBrackets => "]",
        Key.OemPipe => "\\", Key.OemSemicolon => ";", Key.OemQuotes => "'",
        Key.Return => "Enter", Key.Escape => "Esc", Key.Prior => "PageUp", Key.Next => "PageDown",
        Key.Snapshot => "PrintScreen", Key.Scroll => "ScrollLock",
        _ => key.ToString(),
    };

    public static Hotkey Parse(string? text, Hotkey fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        var mods = ModifierKeys.None;
        Key key = Key.None;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "win": case "windows": mods |= ModifierKeys.Windows; break;
                default:
                    if (raw.Length == 1 && char.IsDigit(raw[0])) key = Key.D0 + (raw[0] - '0');
                    else if (raw.StartsWith("num", StringComparison.OrdinalIgnoreCase) && raw.Length == 4 && char.IsDigit(raw[3])) key = Key.NumPad0 + (raw[3] - '0');
                    else if (!Enum.TryParse(raw, ignoreCase: true, out key))
                        key = raw switch
                        {
                            "=" => Key.OemPlus, "-" => Key.OemMinus, "," => Key.OemComma, "." => Key.OemPeriod,
                            "/" => Key.OemQuestion, "`" => Key.OemTilde, "[" => Key.OemOpenBrackets, "]" => Key.OemCloseBrackets,
                            "\\" => Key.OemPipe, ";" => Key.OemSemicolon, "'" => Key.OemQuotes, "Enter" => Key.Return, "Esc" => Key.Escape,
                            _ => Key.None,
                        };
                    break;
            }
        }

        var parsed = new Hotkey(mods, key);
        return parsed.IsValid ? parsed : fallback;
    }

    public static bool IsModifierKey(Key k) =>
        k is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System;
}
