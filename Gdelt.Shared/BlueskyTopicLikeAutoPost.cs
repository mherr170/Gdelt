namespace GdeltSearchUI;

internal static class BlueskyTopicLikeAutoPost
{
    private const string W = "growth";

    private static readonly Dictionary<string, string> TopicBySlug =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gasprices"]   = "gas prices fuel",
            ["quake"]       = "earthquake seismic",
            ["debt"]        = "national debt deficit",
            ["commodity"]   = "crude oil commodities",
            ["yahoo"]       = "energy futures oil prices",
            ["gunviolence"] = "gun violence shooting",
            ["congress"]    = "congress legislation senate",
            ["apod"]        = "astronomy NASA space",
            ["stock"]       = "stock market investing",
            ["weather"]     = "severe weather tornado hurricane",
        };

    public static async Task RunAsync(
        BlueskyFollowClient client, string did, string jwt,
        string slug, string handle, int limit, CancellationToken ct = default)
    {
        if (!TopicBySlug.TryGetValue(slug, out var topic))
        {
            PostLogger.Info(W, $"  [likes] No topic mapped for '{slug}' — skipping");
            return;
        }

        PostLogger.Info(W, $"  [likes] Searching top posts for \"{topic}\"…");

        List<BskyPost> posts;
        try { posts = await client.SearchPostsAsync(topic, jwt, limit: 50, ct); }
        catch (Exception ex) { PostLogger.Error(W, $"  [likes] Search failed: {ex.Message}"); return; }

        var candidates = posts
            .Where(p => !string.IsNullOrEmpty(p.Uri) && !string.IsNullOrEmpty(p.Cid))
            .Where(p => p.Author.Did != did)                        // skip own posts
            .Where(p => !BlueskyLikeTracker.HasLiked(slug, p.Uri))
            .ToList();

        PostLogger.Info(W, $"  [likes] {posts.Count} post(s) returned — {candidates.Count} not yet liked");
        if (candidates.Count == 0) return;

        int liked = 0;
        foreach (var post in candidates)
        {
            if (liked >= limit) break;
            ct.ThrowIfCancellationRequested();

            var (ok, alreadyLiked, error) = await client.LikeAsync(did, post.Uri, post.Cid, jwt, ct);
            if (alreadyLiked) { BlueskyLikeTracker.MarkLiked(slug, post.Uri); continue; }
            if (!ok) { PostLogger.Warn(W, $"  [likes] Failed: {error}"); continue; }

            BlueskyLikeTracker.MarkLiked(slug, post.Uri);
            liked++;
            PostLogger.Success(W, $"  [likes] Liked @{post.Author.Handle}: \"{Truncate(post.Record.Text, 60)}\" ({liked}/{limit})");
            if (liked < limit) await Task.Delay(500, ct);
        }

        PostLogger.Info(W, $"  [likes] {liked} new like(s)");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
