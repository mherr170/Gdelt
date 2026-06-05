namespace GdeltSearchUI;

internal enum GunViolenceAutoPostOutcome { Posted, NoNewArticles, MissingCredentials, Failed, RateLimited }

internal sealed record GunViolenceAutoPostResult(
    GunViolenceAutoPostOutcome Outcome,
    int                        PostedCount  = 0,
    string?                    ErrorMessage = null);

internal static class GunViolenceAutoPost
{
    private const string W          = "gunviolence";
    private const string Query      = "shooting";
    private const int    LookbackH  = 6;
    private const int    MaxPerRun  = 5;

    // 429 retry: 3 attempts with exponential backoff — total wait ~2 min if all fail.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(90)];

    private static readonly string[] FatalKeywords =
        ["killed", "dead", "dies", "died", "fatal", "homicide", "murder", "murdered", "slain", "slaying"];

    private static readonly string[] BlockedDomains = ["foxnews.com"];

    public static async Task<GunViolenceAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, $"Checking GDELT for gun violence articles (past {LookbackH}h)…");

        GdeltSearchResult result;
        using (var client = new GdeltApiClient())
        {
            result = null!;
            var fetched = false;
            var maxAttempts = RetryDelays.Length + 1;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    result = await client.SearchAsync(Query, LookbackH, "ArtList", ct, retryOn429: false);
                }
                catch (OperationCanceledException) { throw; }
                catch (HttpRequestException ex)
                {
                    if (attempt < maxAttempts)
                    {
                        var wait = RetryDelays[attempt - 1];
                        PostLogger.Warn(W, $"Network error (attempt {attempt}/{maxAttempts}), retrying in {wait.TotalSeconds:0}s… ({ex.Message})");
                        await Task.Delay(wait, ct);
                        continue;
                    }
                    PostLogger.Error(W, $"Fetch failed: {ex.Message}");
                    return new(GunViolenceAutoPostOutcome.Failed, ErrorMessage: ex.Message);
                }
                catch (Exception ex)
                {
                    PostLogger.Error(W, $"Fetch failed: {ex.Message}");
                    return new(GunViolenceAutoPostOutcome.Failed, ErrorMessage: ex.Message);
                }

                if (!result.RateLimited) { fetched = true; break; }

                if (attempt < maxAttempts)
                {
                    var wait = RetryDelays[attempt - 1];
                    PostLogger.Warn(W, $"Rate limited (429) — attempt {attempt}/{maxAttempts}, retrying in {wait.TotalSeconds:0}s…");
                    await Task.Delay(wait, ct);
                }
            }

            if (!fetched)
            {
                PostLogger.Warn(W, "Still rate-limited after all retries — skipping this cycle.");
                return new(GunViolenceAutoPostOutcome.RateLimited);
            }
        }

        if (!result.IsSuccess)
        {
            var reason = result.TimedOut ? "request timed out" : result.ErrorMessage ?? "unknown error";
            PostLogger.Error(W, $"GDELT error: {reason}");
            return new(GunViolenceAutoPostOutcome.Failed, ErrorMessage: reason);
        }

        // Stage 1: source country + keyword filter
        var candidates = result.Articles
            .Where(a => a.SourceCountry.Equals("United States", StringComparison.OrdinalIgnoreCase))
            .Where(a => !BlockedDomains.Contains(a.Domain, StringComparer.OrdinalIgnoreCase))
            .Where(a => FatalKeywords.Any(k => a.Title.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Where(a => !GunViolencePostTracker.HasBeenPosted(a.Url))
            .Where(a => !GunViolencePostTracker.HasSimilarIncidentBeenPosted(a.Title))
            .Take(MaxPerRun * 3) // fetch extras so LLM rejection doesn't starve us
            .ToList();

        PostLogger.Info(W,
            $"{result.Articles.Count} articles returned — " +
            $"{candidates.Count} passed country+keyword filter");

        if (candidates.Count == 0)
        {
            PostLogger.Info(W, "No new articles passed filters");
            return new(GunViolenceAutoPostOutcome.NoNewArticles);
        }

        // Stage 2: LLM deduplication — collapse articles covering the same incident
        if (candidates.Count > 1)
        {
            var before = candidates.Count;
            candidates = await LmStudioPostGenerator.DeduplicateBySameEventAsync(candidates);
            var dropped = before - candidates.Count;
            if (dropped > 0)
                PostLogger.Info(W, $"Dedup removed {dropped} duplicate(s) — {candidates.Count} unique incident(s) remain");
        }

        var creds = CredentialManager.LoadGunViolenceBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, $"{candidates.Count} candidate(s) found but no Bluesky credentials configured");
            return new(GunViolenceAutoPostOutcome.MissingCredentials);
        }

        int posted = 0;
        using var poster = new BlueskyPoster();

        for (int i = 0; i < candidates.Count; i++)
        {
            var article = candidates[i];
            if (posted >= MaxPerRun) break;
            ct.ThrowIfCancellationRequested();

            // Stage 3: LLM homicide verification
            PostLogger.Info(W, $"LLM classifying: \"{article.Title}\"");
            var isHomicide = await LmStudioPostGenerator.IsUSGunHomicideAsync(article.Title);
            if (!isHomicide)
            {
                PostLogger.Info(W, $"LLM rejected: \"{article.Title}\"");
                continue;
            }

            var (headline, tags) = await LmStudioPostGenerator.GenerateAsync(article.Title);
            var hashtagLine = BlueskyPostHelper.HashtagLine(tags);
            var text = $"{headline}{hashtagLine}";

            var (ok, error) = await poster.PostAsync(
                creds.Value.Handle, creds.Value.Password, text, article.Url, ct);

            if (ok)
            {
                GunViolencePostTracker.MarkPosted(article.Url, article.Title);
                posted++;
                PostLogger.Success(W, $"Posted: \"{headline}\" | {article.Url}");
            }
            else
            {
                PostLogger.Error(W, $"Post failed for \"{article.Title}\": {error}");
            }

            if (posted < MaxPerRun && i < candidates.Count - 1)
                await Task.Delay(2000, ct);
        }

        if (posted == 0)
        {
            PostLogger.Info(W, "All candidates rejected by LLM — nothing posted");
            return new(GunViolenceAutoPostOutcome.NoNewArticles);
        }

        return new(GunViolenceAutoPostOutcome.Posted, posted);
    }
}
