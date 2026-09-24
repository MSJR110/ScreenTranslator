using System.Windows.Input;
using System.Windows.Interop;
using ScreenTranslator.Native;

namespace ScreenTranslator.Services;

/// <summary>Global hotkeys via RegisterHotKey on a hidden message-only window.</summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = new();
    private int _nextId = 1;

    public HotkeyManager()
    {
        var p = new HwndSourceParameters("ScreenTranslator.Hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = NativeMethods.HWND_MESSAGE,
        };
        _source = new HwndSource(p);
        _source.AddHook(WndProc);
    }

    public bool Register(ModifierKeys modifiers, Key key, Action action)
    {
        uint mods = NativeMethods.MOD_NOREPEAT;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= NativeMethods.MOD_WIN;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_source.Handle, id, mods, vk))
            return false;

        _actions[id] = action;
        return true;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            action();
        }
        return IntPtr.Zero;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
            NativeMethods.UnregisterHotKey(_source.Handle, id);
        _actions.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
