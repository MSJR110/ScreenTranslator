using System.Security.Cryptography;
using System.Text;

namespace ScreenTranslator.Services;

/// <summary>API keys are stored encrypted with Windows DPAPI (per user), never in plain text.</summary>
public static class SecretStore
{
    private const string Prefix = "dpapi:";

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(bytes);
        }
        catch { return plain; }
    }

    public static string Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal)) return stored;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(stored[Prefix.Length..]), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }
}
