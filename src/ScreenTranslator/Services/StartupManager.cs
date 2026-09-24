using Microsoft.Win32;

namespace ScreenTranslator.Services;

/// <summary>Registers/unregisters the app in HKCU Run so it starts with Windows.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ScreenTranslator";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    /// <summary>If autostart is on but points at an old location (the exe moved or was updated), repoint it.</summary>
    public static void RepairIfStale()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(ValueName) is not string current) return;
            var exe = Environment.ProcessPath;
            if (exe is null) return;
            var expected = $"\"{exe}\"";
            if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, expected);
        }
        catch { /* not critical */ }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve executable path.");
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
