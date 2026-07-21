using System.ServiceModel.Syndication;
using System.Xml;

namespace GdeltSearchUI;

// Generic RSS/Atom reader — no API key, no rate limit, no publication lag (unlike
// GNews). Used alongside GDELT to widen coverage and as a fallback tier when GDELT
// is rate-limited. Callers apply the same keyword/dedup filtering as any other
// source; feeds carry no source-country field so it's supplied by the caller.
public sealed record RssSearchResult
{
    public List<GdeltArticle> Articles     { get; init; } = [];
    public string?            ErrorMessage { get; init; }

    public bool IsSuccess => ErrorMessage is null;
}

public sealed class RssApiClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    // Fetches multiple feeds and merges them, keeping only items within the lookback
    // window and deduping by URL. A single broken/slow feed is logged and skipped
    // rather than failing the whole call.
    public async Task<RssSearchResult> FetchAsync(
        IReadOnlyList<string> feedUrls, string sourceCountry, int lookbackHours, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-lookbackHours);
        var articles = new List<GdeltArticle>();
        var failures = 0;

        foreach (var feedUrl in feedUrls)
        {
            try
            {
                using var stream = await _http.GetStreamAsync(feedUrl, ct);
                using var reader = XmlReader.Create(stream);
                var feed = SyndicationFeed.Load(reader);

                foreach (var item in feed.Items)
                {
                    var published = item.PublishDate != default ? item.PublishDate : item.LastUpdatedTime;
                    if (published != default && published < cutoff) continue;

                    var link = item.Links.FirstOrDefault()?.Uri?.ToString();
                    if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(item.Title?.Text))
                        continue;

                    var domain = Uri.TryCreate(link, UriKind.Absolute, out var uri)
                        ? uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host
                        : "";

                    articles.Add(new GdeltArticle
                    {
                        Url           = link,
                        Title         = item.Title.Text,
                        SeenDate      = (published == default ? DateTimeOffset.UtcNow : published).ToString("yyyyMMddTHHmmssZ"),
                        Domain        = domain,
                        Language      = "English",
                        SourceCountry = sourceCountry,
                        Tone          = 0,
                    });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failures++;
                AppLogger.Log($"RSS fetch failed for {feedUrl}: {ex.Message}");
            }
        }

        if (feedUrls.Count > 0 && failures == feedUrls.Count)
            return new RssSearchResult { ErrorMessage = "All RSS feeds failed" };

        var deduped = articles
            .GroupBy(a => a.Url)
            .Select(g => g.First())
            .ToList();

        return new RssSearchResult { Articles = deduped };
    }

    public void Dispose() => _http.Dispose();
}
