namespace GdeltSearchUI;

internal enum BirdAutoPostOutcome
{
    Posted,
    AlreadyPosted,
    MissingApiKey,
    MissingCredentials,
    NoVideosFound,
    Failed,
}

internal sealed record BirdAutoPostResult(
    BirdAutoPostOutcome Outcome,
    string?             SlotKey      = null,
    int                 PostedCount  = 0,
    string?             ErrorMessage = null);

internal static class BirdAutoPost
{
    private const string W             = "bird";
    private const string ChannelHandle = "backyardbirdsofnewjersey";
    private const int    TopN          = 3;

    private static readonly HttpClient _thumbHttp = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<BirdAutoPostResult> PostIfNeededAsync(
        string slotKey, CancellationToken ct = default)
    {
        var apiKey = CredentialManager.LoadYouTubeApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PostLogger.Warn(W, "No YouTube API key configured");
            return new(BirdAutoPostOutcome.MissingApiKey, slotKey);
        }

        var creds = CredentialManager.LoadBirdBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(BirdAutoPostOutcome.MissingCredentials, slotKey);
        }

        // Cross-process lock so a manual one-shot run and the live service can never
        // post the same slot concurrently (a FileStream lock has no thread affinity,
        // so unlike a Mutex it safely spans the awaits below).
        using var slotLock = TryAcquireSlotLock();
        if (slotLock is null)
        {
            PostLogger.Info(W, "Another runner holds the bird post lock — skipping");
            return new(BirdAutoPostOutcome.AlreadyPosted, slotKey);
        }

        // Re-check inside the lock: the holder may have just marked this slot posted.
        if (BirdPostTracker.HasBeenPosted(slotKey))
        {
            PostLogger.Info(W, $"Already posted for slot {slotKey} — skipping");
            return new(BirdAutoPostOutcome.AlreadyPosted, slotKey);
        }

        PostLogger.Info(W, $"Fetching top {TopN} videos for @{ChannelHandle}…");
        IReadOnlyList<YouTubeVideo> videos;
        try
        {
            using var client = new YouTubeApiClient(apiKey);
            videos = await client.GetTopVideosByViewCountAsync(ChannelHandle, TopN, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"YouTube fetch failed: {ex.Message}");
            return new(BirdAutoPostOutcome.Failed, slotKey, ErrorMessage: ex.Message);
        }

        if (videos.Count == 0)
        {
            PostLogger.Warn(W, "No videos returned from YouTube API");
            return new(BirdAutoPostOutcome.NoVideosFound, slotKey);
        }

        PostLogger.Info(W, $"Top {videos.Count} videos: {string.Join(", ", videos.Select(v => $"\"{v.Title}\" ({v.ViewCount:N0} views)"))}");

        int posted = 0;
        using var poster = new BlueskyPoster();
        foreach (var video in videos)
        {
            // Best-effort thumbnail download so the post renders a rich link card
            // instead of a bare URL. A failure here is non-fatal — the card still posts.
            byte[]? thumb = null;
            try
            {
                thumb = await _thumbHttp.GetByteArrayAsync(video.ThumbnailUrl, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                PostLogger.Warn(W, $"Thumbnail fetch failed for \"{video.Title}\": {ex.Message}");
            }

            var (ok, error) = await poster.PostExternalLinkAsync(
                creds.Value.Handle, creds.Value.Password,
                text:            video.Title,
                linkUri:         video.WatchUrl,
                cardTitle:       video.Title,
                cardDescription: $"{video.ViewCount:N0} views · YouTube",
                thumbBytes:      thumb, ct);

            if (ok)
            {
                PostLogger.Success(W, $"Posted: \"{video.Title}\" — {video.ViewCount:N0} views");
                posted++;
            }
            else
            {
                PostLogger.Error(W, $"Post failed for \"{video.Title}\": {error}");
            }

            // Brief pause between posts to avoid rate limiting
            if (posted < videos.Count)
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }

        if (posted > 0)
        {
            BirdPostTracker.MarkPosted(slotKey);
            return new(BirdAutoPostOutcome.Posted, slotKey, posted);
        }

        return new(BirdAutoPostOutcome.Failed, slotKey, ErrorMessage: "All posts failed");
    }

    public static string SlotKey(DateTime now, TimeOnly scheduledTime) =>
        $"{now:yyyy-MM-dd}:{scheduledTime:HH:mm}";

    // Returns a held exclusive lock, or null if another process already holds it.
    private static FileStream? TryAcquireSlotLock()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "GdeltAutoPost", "bird_post.lock");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException)
        {
            return null; // held by another process
        }
    }
}
