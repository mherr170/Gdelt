namespace GdeltSearchUI;

internal static class BlueskyFollowerLogger
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost");

    public static async Task LogAsync(
        BlueskyFollowClient client, string slug, string did, string jwt, CancellationToken ct = default)
    {
        BskyProfile profile;
        try { profile = await client.GetProfileAsync(did, jwt, ct); }
        catch (Exception ex)
        {
            AppLogger.Log($"BlueskyFollowerLogger: failed to fetch profile for {slug}: {ex.Message}");
            return;
        }

        var path   = Path.Combine(_dir, $"followers_{slug}.csv");
        var isNew  = !File.Exists(path);

        Directory.CreateDirectory(_dir);
        if (isNew)
            await File.AppendAllTextAsync(path, "date,followers,following,posts\n", ct);

        var line = $"{DateTime.UtcNow:yyyy-MM-dd},{profile.FollowersCount},{profile.FollowsCount},{profile.PostsCount}";
        await File.AppendAllTextAsync(path, line + "\n", ct);

        AppLogger.Log($"BlueskyFollowerLogger [{slug}]: {profile.FollowersCount} followers logged.");
    }
}
