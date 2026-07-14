namespace GdeltSearchUI;

internal enum GrowthOutcome { Ok, AuthFailed }

internal sealed record GrowthResult(
    GrowthOutcome Outcome,
    string?       ErrorMessage = null);

internal static class BlueskyGrowthAutoPost
{
    private const string W = "growth";

    public static async Task<GrowthResult> RunAsync(
        string label, string slug, string handle, string password,
        int dailyFollowLimit, int dailyLikeLimit, int unfollowGraceDays,
        CancellationToken ct = default)
    {
        PostLogger.Info(W, $"--- @{handle} ({label}) ---");

        using var client = new BlueskyFollowClient();

        var (did, jwt, authError) = await client.AuthenticateAsync(handle, password, ct);
        if (authError is not null)
        {
            PostLogger.Error(W, $"Auth failed: {authError}");
            return new(GrowthOutcome.AuthFailed, authError);
        }

        // Log follower count for growth tracking (fire-and-forget style — don't block on failure).
        await BlueskyFollowerLogger.LogAsync(client, slug, did, jwt, ct);

        // Suggestion-follows and follow-backs share one daily follow budget rather
        // than each getting the full limit — otherwise an account can gain up to
        // 2x dailyFollowLimit in a day, which isn't what the config value implies.
        var suggestionsFollowed = await RunSuggestionFollowsAsync(client, did, jwt, slug, handle, dailyFollowLimit, ct);
        var remainingBudget     = Math.Max(0, dailyFollowLimit - suggestionsFollowed);
        await BlueskyFollowBackAutoPost.RunAsync(client, did, jwt, slug, handle, remainingBudget, ct);
        await BlueskyUnfollowAutoPost.RunAsync(client, did, jwt, slug, unfollowGraceDays, ct);
        await BlueskyTopicLikeAutoPost.RunAsync(client, did, jwt, slug, handle, dailyLikeLimit, ct);

        return new(GrowthOutcome.Ok);
    }

    private static async Task<int> RunSuggestionFollowsAsync(
        BlueskyFollowClient client, string did, string jwt,
        string slug, string handle, int limit, CancellationToken ct)
    {
        PostLogger.Info(W, "  [suggestions] Fetching suggested accounts…");

        List<SuggestedActor> suggestions;
        try { suggestions = await client.GetSuggestionsAsync(jwt, limit: 100, ct); }
        catch (Exception ex) { PostLogger.Error(W, $"  [suggestions] Failed: {ex.Message}"); return 0; }

        var candidates = suggestions
            .Where(a => !string.IsNullOrEmpty(a.Did) && !BlueskyFollowTracker.HasFollowed(slug, a.Did))
            .ToList();

        PostLogger.Info(W, $"  [suggestions] {suggestions.Count} returned — {candidates.Count} not yet followed");
        if (candidates.Count == 0) return 0;

        int followed = 0;
        foreach (var actor in candidates)
        {
            if (followed >= limit) break;
            ct.ThrowIfCancellationRequested();

            var (ok, alreadyFollowing, error) = await client.FollowAsync(did, actor.Did, jwt, ct);
            if (alreadyFollowing) { BlueskyFollowTracker.MarkFollowed(slug, actor.Did); continue; }
            if (!ok) { PostLogger.Warn(W, $"  [suggestions] Failed @{actor.Handle}: {error}"); continue; }

            BlueskyFollowTracker.MarkFollowed(slug, actor.Did);
            followed++;
            PostLogger.Success(W, $"  [suggestions] Followed @{actor.Handle} ({followed}/{limit})");
            if (followed < limit) await Task.Delay(1000, ct);
        }

        PostLogger.Info(W, $"  [suggestions] {followed} new follow(s)");
        return followed;
    }
}
