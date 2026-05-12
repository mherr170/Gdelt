using System.Security.Cryptography;
using System.Text;

namespace GdeltSearchUI;

// Stores credentials encrypted with DPAPI LocalMachine scope so any process
// on this machine (both the UI and the Windows service) can read them.
internal static class SecretStore
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "secrets");

    public static void Save(string key, string username, string password)
    {
        Directory.CreateDirectory(_dir);
        var combined = $"{username}\n{password}";
        var plain    = Encoding.UTF8.GetBytes(combined);
        var cipher   = ProtectedData.Protect(plain, null, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(Path.Combine(_dir, Filename(key)), cipher);
    }

    public static (string Username, string Password)? Load(string key)
    {
        var path = Path.Combine(_dir, Filename(key));
        if (!File.Exists(path)) return null;
        try
        {
            var cipher   = File.ReadAllBytes(path);
            var plain    = ProtectedData.Unprotect(cipher, null, DataProtectionScope.LocalMachine);
            var combined = Encoding.UTF8.GetString(plain);
            var sep      = combined.IndexOf('\n');
            if (sep < 0) return null;
            return (combined[..sep], combined[(sep + 1)..]);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"SecretStore load failed ({key}): {ex.Message}");
            return null;
        }
    }

    public static void Delete(string key)
    {
        var path = Path.Combine(_dir, Filename(key));
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private static string Filename(string key) =>
        string.Concat(key.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')) + ".dat";
}
