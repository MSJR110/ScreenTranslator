using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenTranslator.Services;

namespace ScreenTranslator.UI;

/// <summary>Click, press a chord, done. Backspace/Delete clears; Esc cancels the edit.</summary>
public sealed class HotkeyBox : Border
{
    private readonly TextBlock _text = new()
    {
        FontFamily = Theme.AppFont,
        FontSize = 12.5,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        FlowDirection = FlowDirection.LeftToRight,
    };

    private Hotkey _value;
    private bool _capturing;

    public event Action<Hotkey>? ValueChanged;

    public Hotkey Value
    {
        get => _value;
        set { _value = value; Render(); }
    }

    public HotkeyBox()
    {
        Child = _text;
        MinWidth = 140;
        Height = 30;
        CornerRadius = new CornerRadius(7);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(10, 0, 10, 0);
        Cursor = Cursors.Hand;
        Focusable = true;
        FocusVisualStyle = null;
        SetResourceReference(BackgroundProperty, "InputBg");
        SetResourceReference(BorderBrushProperty, "Border");
        _text.SetResourceReference(TextBlock.ForegroundProperty, "Text");

        MouseLeftButtonDown += (_, e) => { Focus(); e.Handled = true; };
        GotKeyboardFocus += (_, _) => { _capturing = true; Render(); };
        LostKeyboardFocus += (_, _) => { _capturing = false; Render(); };
        PreviewKeyDown += OnKey;
        Render();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { Keyboard.ClearFocus(); return; }
        if (key is Key.Back or Key.Delete) { Set(Hotkey.None); Keyboard.ClearFocus(); return; }
        if (Hotkey.IsModifierKey(key)) { Render(Keyboard.Modifiers); return; }

        var chord = new Hotkey(Keyboard.Modifiers, key);
        if (!chord.IsValid) { Render(Keyboard.Modifiers); return; }

        Set(chord);
        Keyboard.ClearFocus();
    }

    private void Set(Hotkey h)
    {
        if (h == _value) return;
        _value = h;
        ValueChanged?.Invoke(h);
        Render();
    }

    private void Render(ModifierKeys? pending = null)
    {
        if (_capturing)
        {
            var mods = pending ?? ModifierKeys.None;
            var modsText = Hotkey.ModifiersToString(mods);
            _text.Text = modsText.Length == 0 ? "…" : modsText + "+…";
            _text.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
            SetResourceReference(BorderBrushProperty, "Accent");
            return;
        }

        _text.Text = _value.IsEmpty ? "—" : _value.ToString();
        _text.SetResourceReference(TextBlock.ForegroundProperty, _value.IsEmpty ? "Muted" : "Text");
        SetResourceReference(BorderBrushProperty, "Border");
    }
}
