namespace GdeltSearchUI;

// Writes structured operational logs for each auto-post widget.
// Files: %ProgramData%\GdeltAutoPost\logs\{widget}_autopost.log
// Rolls over to .bak at 500 KB so logs stay readable.
internal static class PostLogger
{
    private const long RolloverBytes = 512 * 1024;

    public static void Info(string widget, string message)    => Write(widget, "INFO   ", message);
    public static void Success(string widget, string message) => Write(widget, "SUCCESS", message);
    public static void Warn(string widget, string message)    => Write(widget, "WARN   ", message);
    public static void Error(string widget, string message)   => Write(widget, "ERROR  ", message);

    private static string FilePath(string widget) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "logs", $"{widget.ToLowerInvariant()}_autopost.log");

    private static void Write(string widget, string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} | {message}";
        var path = FilePath(widget);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Rollover(path);
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch { /* never crash on a log write */ }

        LiveActivityBroadcaster.Publish(widget, level.Trim(), message);
    }

    public static void Clear(string widget)
    {
        try { File.Delete(FilePath(widget)); }
        catch { }
    }

    // Clears the log file on a repeating interval. Fire-and-forget from each worker.
    public static async Task ClearPeriodicallyAsync(string widget, TimeSpan interval, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval);
        while (true)
        {
            try   { if (!await timer.WaitForNextTickAsync(ct)) break; }
            catch (OperationCanceledException)                 { break; }
            Clear(widget);
        }
    }

    private static void Rollover(string path)
    {
        if (!File.Exists(path)) return;
        if (new FileInfo(path).Length < RolloverBytes) return;
        var bak = path + ".bak";
        if (File.Exists(bak)) File.Delete(bak);
        File.Move(path, bak);
    }
}
