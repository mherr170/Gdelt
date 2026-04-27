using System.Diagnostics;

namespace GdeltSearchUI;

internal static class AppLogger
{
    public static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "gdelt_debug.log");

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(line);
        try { File.AppendAllText(LogPath, line + Environment.NewLine); }
        catch { /* never crash the app over a log write */ }
    }
}
