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

    // GNews free tier delays article availability by up to ~12h, so the fallback
    // needs a much wider window than GDELT's to actually surface anything —
    // it's filling gaps left by earlier rate-limited cycles, not this cycle's news.
    private const int GNewsLookbackH = 24;

    // RSS has no publication lag, so it uses the same window as GDELT rather than
    // GNews's widened one. Tried both as a fallback (ahead of GNews, when GDELT is
    // rate-limited) and as a supplement merged into a successful GDELT result, to
    // catch outlets/stories GDELT's own index missed this cycle.
    private const int RssLookbackH = 6;

    // CNN's public RSS feeds (rss.cnn.com, cnn.com/rss/edition_us.rss) are dead —
    // stale since 2024 and 2012 respectively — so they're left out here.
    //
    // GDELT direct fetches 429 on ~80% of cycles (see RetryDelays below), so this
    // list is the de facto primary source most cycles, not just an occasional
    // fallback — kept wide accordingly.
    private static readonly string[] RssFeeds =
    [
        "https://www.cbsnews.com/latest/rss/crime",
        "https://www.cbsnews.com/latest/rss/us",
        "https://abcnews.go.com/abcnews/usheadlines",
        "https://feeds.nbcnews.com/nbcnews/public/news",
        "https://feeds.npr.org/1003/rss.xml",
        "https://rssfeeds.usatoday.com/usatoday-NewsTopStories",
        "https://rss.nytimes.com/services/xml/rss/nyt/US.xml",
        "https://www.theguardian.com/us-news/rss",
    ];

    // 429 retry: single 30s retry, then fall back to RSS. GDELT's public endpoint
    // 429s on the first attempt ~80% of the time, and a second retry after 90s
    // almost never recovers it — so that second wait was pure delay before the
    // RSS fallback that ends up serving the cycle anyway.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(30)];

    private static readonly string[] FatalKeywords =
        ["killed", "dead", "dies", "died", "fatal", "homicide", "murder", "murdered", "slain", "slaying"];

    private static readonly string[] BlockedDomains = ["foxnews.com", "breitbart.com"];

    public static async Task<GunViolenceAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, $"Checking GDELT for gun violence articles (past {LookbackH}h)…");

        GdeltSearchResult result;
        bool fetched;
        using (var client = new GdeltApiClient())
        {
            result = null!;
            fetched = false;
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
                PostLogger.Warn(W, "Still rate-limited after all retries — trying RSS fallback…");
                var rssFallback = await TryRssFallbackAsync(ct);
                if (rssFallback is { Articles.Count: > 0 })
                {
                    result = rssFallback;
                }
                else
                {
                    PostLogger.Warn(W, "RSS fallback returned nothing — trying GNews fallback…");
                    var gnewsFallback = await TryGNewsFallbackAsync(ct);
                    if (gnewsFallback is null)
                        return new(GunViolenceAutoPostOutcome.RateLimited);
                    result = gnewsFallback;
                }
            }
        }

        if (!result.IsSuccess)
        {
            var reason = result.TimedOut ? "request timed out" : result.ErrorMessage ?? "unknown error";
            PostLogger.Error(W, $"GDELT error: {reason}");
            return new(GunViolenceAutoPostOutcome.Failed, ErrorMessage: reason);
        }

        // Supplement: merge in RSS coverage alongside a successful GDELT result —
        // catches outlets/stories GDELT's own index missed this cycle. Skipped when
        // `result` already came from the RSS/GNews fallback path above.
        if (fetched)
        {
            var supplement = await TryRssFallbackAsync(ct);
            if (supplement is { Articles.Count: > 0 })
            {
                var before = result.Articles.Count;
                result = result with
                {
                    Articles = result.Articles
                        .Concat(supplement.Articles)
                        .GroupBy(a => a.Url)
                        .Select(g => g.First())
                        .ToList(),
                };
                PostLogger.Info(W, $"RSS supplement added {result.Articles.Count - before} article(s) (total {result.Articles.Count})");
            }
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
        // within this batch (same polling cycle).
        if (candidates.Count > 1)
        {
            var before = candidates.Count;
            candidates = await LmStudioPostGenerator.DeduplicateBySameEventAsync(candidates);
            var dropped = before - candidates.Count;
            if (dropped > 0)
                PostLogger.Info(W, $"Dedup removed {dropped} duplicate(s) — {candidates.Count} unique incident(s) remain");
        }

        // Stage 2.5: LLM check against recently-posted incidents — catches the same
        // incident covered by a different outlet in an earlier polling cycle, which
        // Stage 2 misses (it only compares within the current batch) and the lexical
        // filter above can miss when outlets phrase the story very differently.
        var recentTitles = GunViolencePostTracker.GetRecentPostedTitles();
        if (recentTitles.Count > 0 && candidates.Count > 0)
        {
            var kept = new List<GdeltArticle>();
            foreach (var article in candidates)
            {
                var isDup = await LmStudioPostGenerator.IsDuplicateOfRecentPostsAsync(article.Title, recentTitles, ct);
                if (isDup)
                    PostLogger.Info(W, $"Duplicate of a recent post, skipping: \"{article.Title}\"");
                else
                    kept.Add(article);
            }
            candidates = kept;
        }

        if (candidates.Count == 0)
        {
            PostLogger.Info(W, "All candidates were duplicates of recently posted incidents");
            return new(GunViolenceAutoPostOutcome.NoNewArticles);
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

    // Returns a GdeltSearchResult (possibly with an empty Articles list) on success,
    // or null if the fetch itself failed. No API key, no rate limit — callers treat
    // an empty result the same as "unavailable this cycle" and fall through further.
    private static async Task<GdeltSearchResult?> TryRssFallbackAsync(CancellationToken ct)
    {
        using var rss = new RssApiClient();
        RssSearchResult result;
        try
        {
            result = await rss.FetchAsync(RssFeeds, "United States", RssLookbackH, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"RSS fallback failed: {ex.Message}");
            return null;
        }

        if (!result.IsSuccess)
        {
            PostLogger.Warn(W, $"RSS fallback unavailable: {result.ErrorMessage}");
            return null;
        }

        PostLogger.Info(W, $"RSS returned {result.Articles.Count} article(s).");
        return new GdeltSearchResult { Articles = result.Articles };
    }

    // Returns a GdeltSearchResult to fall into the normal pipeline on success, or
    // null if no key is configured or the fallback itself failed (caller should
    // then report RateLimited, same as if no fallback existed).
    private static async Task<GdeltSearchResult?> TryGNewsFallbackAsync(CancellationToken ct)
    {
        var apiKey = CredentialManager.LoadGNewsApiKey();
        if (apiKey is null)
        {
            PostLogger.Warn(W, "No GNews API key configured — skipping this cycle.");
            return null;
        }

        using var gnews = new GNewsApiClient();
        GNewsSearchResult fallbackResult;
        try
        {
            fallbackResult = await gnews.SearchAsync(apiKey, Query, GNewsLookbackH, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"GNews fallback failed: {ex.Message}");
            return null;
        }

        if (fallbackResult.QuotaExceeded)
        {
            PostLogger.Warn(W, "GNews daily quota exceeded — skipping this cycle.");
            return null;
        }

        if (!fallbackResult.IsSuccess)
        {
            PostLogger.Warn(W, $"GNews fallback unavailable — skipping this cycle. ({fallbackResult.ErrorMessage ?? "rate limited"})");
            return null;
        }

        PostLogger.Info(W, $"GNews fallback returned {fallbackResult.Articles.Count} article(s).");
        return new GdeltSearchResult { Articles = fallbackResult.Articles };
    }
}
