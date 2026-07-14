namespace GdeltSearchUI;

internal static class BlueskyFollowBackAutoPost
{
    private const string W = "growth";

    public static async Task RunAsync(
        BlueskyFollowClient client, string did, string jwt,
        string slug, string handle, int limit, CancellationToken ct = default)
    {
        PostLogger.Info(W, "  [follow-back] Fetching followers and following lists…");

        List<SuggestedActor> followers;
        HashSet<string> following;
        try
        {
            followers = await client.GetFollowersAsync(did, jwt, maxResults: BlueskyFollowClient.MaxRelationshipFetch, ct);
            following  = await client.GetFollowDidsAsync(did, jwt, maxResults: BlueskyFollowClient.MaxRelationshipFetch, ct);
        }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"  [follow-back] Fetch failed: {ex.Message}");
            return;
        }

        var toFollow = followers
            .Where(f => !string.IsNullOrEmpty(f.Did)
                     && !following.Contains(f.Did)
                     && !BlueskyFollowTracker.HasFollowed(slug, f.Did))
            .ToList();

        PostLogger.Info(W,
            $"  [follow-back] {followers.Count} follower(s), {following.Count} following — " +
            $"{toFollow.Count} to follow back");

        if (toFollow.Count == 0) return;

        int followed = 0;
        foreach (var actor in toFollow)
        {
            if (followed >= limit) break;
            ct.ThrowIfCancellationRequested();

            var (ok, alreadyFollowing, error) = await client.FollowAsync(did, actor.Did, jwt, ct);
            if (alreadyFollowing) { BlueskyFollowTracker.MarkFollowed(slug, actor.Did); continue; }
            if (!ok) { PostLogger.Warn(W, $"  [follow-back] Failed @{actor.Handle}: {error}"); continue; }

            BlueskyFollowTracker.MarkFollowed(slug, actor.Did);
            followed++;
            PostLogger.Success(W, $"  [follow-back] Followed back @{actor.Handle} ({followed}/{limit})");
            if (followed < limit) await Task.Delay(1000, ct);
        }

        PostLogger.Info(W, $"  [follow-back] {followed} new follow-back(s)");
    }
}
