namespace GdeltSearchUI;

internal enum OutbreakAutoPostOutcome { Posted, NoNewArticles, MissingCredentials, Failed }

internal sealed record OutbreakAutoPostResult(
    OutbreakAutoPostOutcome Outcome,
    int                     PostedCount  = 0,
    string?                 ErrorMessage = null);

// Outbreak Watch: global disease-outbreak news from health/world RSS feeds (primary)
// plus a best-effort GDELT query. Candidates pass a keyword filter, then an LLM
// check that the headline reports a real outbreak development, then duplicate checks.
// Unlike the other news bots this one fails closed when the LLM is down — an
// unverified health scare is worse than a missed post.
internal static class OutbreakAutoPost
{
    private const string W         = "outbreak";
    private const string Query     = "(outbreak OR epidemic OR pandemic OR plague OR quarantine)";
    // Wide window: the tracker dedups by URL and headline similarity, so re-seeing a
    // story costs nothing, while a narrow window misses stories that are only briefly
    // in a low-volume feed. GDELT 429s nearly every cycle, so RSS has to carry this.
    private const int    LookbackH = 24;
    private const int    MaxPerRun = 3;

    private static readonly string[] RssFeeds =
    [
        "https://feeds.bbci.co.uk/news/health/rss.xml",
        "https://www.cidrap.umn.edu/rss.xml",
        "https://outbreaknewstoday.com/feed/",
        "https://rss.nytimes.com/services/xml/rss/nyt/Health.xml",
        "https://feeds.npr.org/1128/rss.xml",
        "https://www.statnews.com/feed/",
        "https://www.ecdc.europa.eu/en/taxonomy/term/1310/feed",

        // General world-news feeds: breaking outbreaks (e.g. a lab-linked death in
        // Siberia) hit these long before the health-desk feeds above pick them up.
        // Volume is fine — the keyword filter and LLM classifier cut it down.
        "https://feeds.bbci.co.uk/news/world/rss.xml",
        "https://rss.nytimes.com/services/xml/rss/nyt/World.xml",
        "https://www.theguardian.com/world/rss",
        "https://www.aljazeera.com/xml/rss/all.xml",
        "https://feeds.npr.org/1004/rss.xml",
        "https://www.cbsnews.com/latest/rss/world",
        "https://abcnews.go.com/abcnews/internationalheadlines",
        "https://rss.dw.com/rdf/rss-en-all",
    ];

    private static readonly string[] Keywords =
    [
        "outbreak", "epidemic", "pandemic", "plague", "quarantine", "ebola", "marburg",
        "cholera", "mpox", "monkeypox", "h5n1", "bird flu", "avian flu", "measles",
        "anthrax", "nipah", "lassa", "dengue", "polio", "public health emergency",
        "health emergency", "mystery illness", "unknown illness", "novel virus",
        "new virus", "new strain", "superbug", "pathogen", "contagion", "cases of",
    ];

    // Tabloids that routinely run "Covid-style" framing on unconfirmed health stories.
    private static readonly string[] BlockedDomains =
        ["dailymail.co.uk", "gbnews.com", "thesun.co.uk", "nypost.com", "express.co.uk"];

    public static async Task<OutbreakAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, $"Checking health RSS feeds + GDELT for outbreak news (past {LookbackH}h)…");

        var articles = new List<GdeltArticle>();
        string? rssError = null, gdeltError = null;

        using (var rss = new RssApiClient())
        {
            try
            {
                var r = await rss.FetchAsync(RssFeeds, "", LookbackH, ct);
                if (r.IsSuccess) articles.AddRange(r.Articles);
                else rssError = r.ErrorMessage;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { rssError = ex.Message; }
        }

        // GDELT is a supplement only: it 429s constantly, so a miss just means RSS carries the cycle.
        using (var client = new GdeltApiClient())
        {
            try
            {
                var g = await client.SearchAsync(Query, LookbackH, "ArtList", ct, retryOn429: false);
                if (g.IsSuccess && !g.RateLimited)
                    articles.AddRange(g.Articles.Where(a => a.Language.Equals("English", StringComparison.OrdinalIgnoreCase)));
                else
                    gdeltError = g.RateLimited ? "rate limited" : g.ErrorMessage ?? "unknown error";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { gdeltError = ex.Message; }
        }

        if (articles.Count == 0 && rssError is not null && gdeltError is not null)
        {
            PostLogger.Error(W, $"All sources failed — RSS: {rssError}; GDELT: {gdeltError}");
            return new(OutbreakAutoPostOutcome.Failed, ErrorMessage: $"RSS: {rssError}; GDELT: {gdeltError}");
        }

        if (gdeltError is not null)
            PostLogger.Info(W, $"GDELT unavailable this cycle ({gdeltError}) — RSS only");

        var candidates = articles
            .GroupBy(a => a.Url).Select(g => g.First())
            .Where(a => !BlockedDomains.Contains(a.Domain, StringComparer.OrdinalIgnoreCase))
            .Where(a => Keywords.Any(k => a.Title.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Where(a => !OutbreakPostTracker.HasBeenPosted(a.Url))
            .Where(a => !OutbreakPostTracker.HasSimilarStoryBeenPosted(a.Title))
            .Take(MaxPerRun * 4) // extras so LLM rejection doesn't starve us
            .ToList();

        PostLogger.Info(W, $"{articles.Count} articles fetched — {candidates.Count} passed keyword filter");

        if (candidates.Count == 0)
            return new(OutbreakAutoPostOutcome.NoNewArticles);

        if (candidates.Count > 1)
        {
            var before = candidates.Count;
            candidates = await LmStudioPostGenerator.DeduplicateOutbreakAsync(candidates, ct);
            if (candidates.Count < before)
                PostLogger.Info(W, $"Dedup removed {before - candidates.Count} duplicate(s)");
        }

        var creds = CredentialManager.LoadOutbreakBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, $"{candidates.Count} candidate(s) found but no Bluesky credentials configured");
            return new(OutbreakAutoPostOutcome.MissingCredentials);
        }

        int posted = 0;
        var recentTitles = OutbreakPostTracker.GetRecentPostedTitles().ToList();
        using var poster = new BlueskyPoster();

        foreach (var article in candidates)
        {
            if (posted >= MaxPerRun) break;
            ct.ThrowIfCancellationRequested();

            if (!await LmStudioPostGenerator.IsOutbreakNewsAsync(article.Title, ct))
            {
                PostLogger.Info(W, $"LLM rejected: \"{article.Title}\"");
                continue;
            }

            if (await LmStudioPostGenerator.IsDuplicateOutbreakAsync(article.Title, recentTitles, ct))
            {
                PostLogger.Info(W, $"Duplicate of a recent post, skipping: \"{article.Title}\"");
                continue;
            }

            var (headline, tags) = await LmStudioPostGenerator.GenerateOutbreakPostAsync(article.Title, ct);
            var text = $"{headline}{BlueskyPostHelper.HashtagLine(tags)}";

            var (ok, error) = await poster.PostAsync(creds.Value.Handle, creds.Value.Password, text, article.Url, ct);

            if (ok)
            {
                OutbreakPostTracker.MarkPosted(article.Url, article.Title);
                recentTitles.Add(article.Title);
                posted++;
                PostLogger.Success(W, $"Posted: \"{headline}\" | {article.Url}");
                if (posted < MaxPerRun) await Task.Delay(2000, ct);
            }
            else
            {
                PostLogger.Error(W, $"Post failed for \"{article.Title}\": {error}");
            }
        }

        if (posted == 0)
        {
            PostLogger.Info(W, "All candidates rejected or duplicate — nothing posted");
            return new(OutbreakAutoPostOutcome.NoNewArticles);
        }

        return new(OutbreakAutoPostOutcome.Posted, posted);
    }
}
