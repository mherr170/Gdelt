namespace GdeltSearchUI;

internal static class BlueskyUnfollowAutoPost
{
    private const string W = "growth";

    public static async Task RunAsync(
        BlueskyFollowClient client, string did, string jwt,
        string slug, int graceDays, CancellationToken ct = default)
    {
        var cutoff     = DateTime.UtcNow.AddDays(-graceDays);
        var candidates = BlueskyFollowTracker.GetFollowedBefore(slug, cutoff);

        if (candidates.Count == 0)
        {
            PostLogger.Info(W, $"  [unfollow] No follows older than {graceDays} days to evaluate");
            return;
        }

        PostLogger.Info(W, $"  [unfollow] {candidates.Count} follow(s) past {graceDays}-day grace — checking reciprocity…");

        HashSet<string> followers;
        try
        {
            var list = await client.GetFollowersAsync(did, jwt, maxResults: 500, ct);
            followers = new HashSet<string>(list.Select(a => a.Did), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"  [unfollow] Failed to fetch followers: {ex.Message}");
            return;
        }

        var toUnfollow = candidates.Where(d => !followers.Contains(d)).ToList();
        PostLogger.Info(W, $"  [unfollow] {toUnfollow.Count} didn't follow back");

        if (toUnfollow.Count == 0) return;

        // Fetch our follow records to get the rkey needed for deletion.
        Dictionary<string, string> rkeys;
        try { rkeys = await client.GetFollowRecordRkeysAsync(did, jwt, ct); }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"  [unfollow] Failed to fetch follow records: {ex.Message}");
            return;
        }

        int unfollowed = 0;
        foreach (var subjectDid in toUnfollow)
        {
            ct.ThrowIfCancellationRequested();

            if (!rkeys.TryGetValue(subjectDid, out var rkey))
                continue; // already unfollowed outside of this system

            var (ok, error) = await client.UnfollowAsync(did, rkey, jwt, ct);
            if (!ok) { PostLogger.Warn(W, $"  [unfollow] Failed ({subjectDid}): {error}"); continue; }

            unfollowed++;
            PostLogger.Success(W, $"  [unfollow] Unfollowed {subjectDid} ({unfollowed}/{toUnfollow.Count})");
            await Task.Delay(1000, ct);
        }

        PostLogger.Info(W, $"  [unfollow] {unfollowed} account(s) removed");
    }
}
