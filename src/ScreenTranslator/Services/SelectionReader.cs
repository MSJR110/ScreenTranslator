using System.Windows;
using ScreenTranslator.Native;

namespace ScreenTranslator.Services;

/// <summary>
/// Grabs the text currently selected in the foreground app by simulating Ctrl+C.
/// Falls back to whatever is already on the clipboard when nothing was selected.
/// </summary>
public static class SelectionReader
{
    public sealed record Result(string Text, bool FromClipboardFallback);

    public static async Task<Result?> ReadAsync()
    {
        string? backup = SafeGetClipboardText();
        uint before = NativeMethods.GetClipboardSequenceNumber();

        // The user is still holding Ctrl+Alt from the hotkey; release them so the target app sees a clean Ctrl+C.
        NativeMethods.SendKeys(
            NativeMethods.KeyInput(NativeMethods.VK_MENU, up: true),
            NativeMethods.KeyInput(NativeMethods.VK_SHIFT, up: true),
            NativeMethods.KeyInput(NativeMethods.VK_LWIN, up: true),
            NativeMethods.KeyInput(NativeMethods.VK_CONTROL, up: true));
        await Task.Delay(30);

        NativeMethods.SendKeys(
            NativeMethods.KeyInput(NativeMethods.VK_CONTROL, up: false),
            NativeMethods.KeyInput(NativeMethods.VK_C, up: false),
            NativeMethods.KeyInput(NativeMethods.VK_C, up: true),
            NativeMethods.KeyInput(NativeMethods.VK_CONTROL, up: true));

        // Wait for the clipboard to actually change (apps vary in how fast they respond).
        bool changed = false;
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(50);
            if (NativeMethods.GetClipboardSequenceNumber() != before) { changed = true; break; }
        }

        if (changed)
        {
            // A few apps need a moment after bumping the sequence before the data is readable.
            await Task.Delay(40);
            string? text = SafeGetClipboardText();

            // Put the user's original clipboard back so we don't clobber it.
            if (backup is not null)
                SafeSetClipboardText(backup);

            if (!string.IsNullOrWhiteSpace(text))
                return new Result(text.Trim(), FromClipboardFallback: false);
        }

        if (!string.IsNullOrWhiteSpace(backup))
            return new Result(backup.Trim(), FromClipboardFallback: true);

        return null;
    }

    private static string? SafeGetClipboardText()
    {
        for (int i = 0; i < 5; i++)
        {
            try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
            catch { Thread.Sleep(20); }
        }
        return null;
    }

    private static void SafeSetClipboardText(string text)
    {
        for (int i = 0; i < 5; i++)
        {
            try { Clipboard.SetDataObject(text, true); return; }
            catch { Thread.Sleep(20); }
        }
    }
}
