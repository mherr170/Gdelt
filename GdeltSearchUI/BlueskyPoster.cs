using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

internal sealed class BlueskyPoster : IDisposable
{
    private const string BaseUrl = "https://bsky.social/xrpc";
    private const int MaxPostChars = 300;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<(bool Ok, string? Error)> PostAsync(
        string handle, string appPassword, string title, string url, CancellationToken ct)
    {
        // 1. Authenticate
        var (session, authError) = await AuthenticateAsync(handle, appPassword, ct);
        if (authError is not null) return (false, authError);

        // 2. Build post text + link facet
        var (text, facets) = await BuildPostAsync(title, url);

        // 3. Create record
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(new
            {
                repo = session.Did,
                collection = "app.bsky.feed.post",
                record = new PostRecord
                {
                    Text = text,
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Langs = ["en"],
                    Facets = facets,
                },
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

        var postResp = await _http.SendAsync(req, ct);
        if (!postResp.IsSuccessStatusCode)
        {
            var body = await postResp.Content.ReadAsStringAsync(ct);
            return (false, $"Post failed ({(int)postResp.StatusCode}):\n{body}");
        }

        return (true, null);
    }

    // Posts pre-built text together with a single attached image.
    public async Task<(bool Ok, string? Error)> PostTextWithImageAsync(
        string handle, string appPassword, string text,
        byte[] imageBytes, string altText, CancellationToken ct)
    {
        var (session, authError) = await AuthenticateAsync(handle, appPassword, ct);
        if (authError is not null) return (false, authError);

        var si = new System.Globalization.StringInfo(text);
        if (si.LengthInTextElements > MaxPostChars)
            text = si.SubstringByTextElements(0, MaxPostChars - 1) + "…";

        // 1. Upload image as a blob.
        using var blobReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.uploadBlob")
        {
            Content = new ByteArrayContent(imageBytes),
        };
        blobReq.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        blobReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

        var blobResp = await _http.SendAsync(blobReq, ct);
        if (!blobResp.IsSuccessStatusCode)
        {
            var body = await blobResp.Content.ReadAsStringAsync(ct);
            return (false, $"Image upload failed ({(int)blobResp.StatusCode}):\n{body}");
        }
        var blobJson = await blobResp.Content.ReadFromJsonAsync<UploadBlobResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty blob response.");

        // 2. Create record with image embed.
        var embed = new
        {
            type   = "app.bsky.embed.images",
            images = new[]
            {
                new { alt = altText, image = blobJson.Blob },
            },
        };

        var record = new Dictionary<string, object?>
        {
            ["$type"]     = "app.bsky.feed.post",
            ["text"]      = text,
            ["createdAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["langs"]     = new[] { "en" },
            ["facets"]    = new List<Facet>(),
            ["embed"]     = new Dictionary<string, object?>
            {
                ["$type"]  = "app.bsky.embed.images",
                ["images"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["alt"]   = altText,
                        ["image"] = blobJson.Blob,
                    },
                },
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(new
            {
                repo       = session.Did,
                collection = "app.bsky.feed.post",
                record,
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

        var postResp = await _http.SendAsync(req, ct);
        if (!postResp.IsSuccessStatusCode)
        {
            var body = await postResp.Content.ReadAsStringAsync(ct);
            return (false, $"Post failed ({(int)postResp.StatusCode}):\n{body}");
        }

        return (true, null);
    }

    // Posts pre-built text with no URL or hashtag processing — used for non-article posts.
    public async Task<(bool Ok, string? Error)> PostTextAsync(
        string handle, string appPassword, string text, CancellationToken ct)
    {
        var (session, authError) = await AuthenticateAsync(handle, appPassword, ct);
        if (authError is not null) return (false, authError);

        var si = new System.Globalization.StringInfo(text);
        if (si.LengthInTextElements > MaxPostChars)
            text = si.SubstringByTextElements(0, MaxPostChars - 1) + "…";

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(new
            {
                repo = session.Did,
                collection = "app.bsky.feed.post",
                record = new PostRecord
                {
                    Text = text,
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Langs = ["en"],
                    Facets = new List<Facet>(),
                },
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

        var postResp = await _http.SendAsync(req, ct);
        if (!postResp.IsSuccessStatusCode)
        {
            var body = await postResp.Content.ReadAsStringAsync(ct);
            return (false, $"Post failed ({(int)postResp.StatusCode}):\n{body}");
        }

        return (true, null);
    }

    private async Task<(SessionResponse Session, string? Error)> AuthenticateAsync(
        string handle, string appPassword, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync(
            $"{BaseUrl}/com.atproto.server.createSession",
            new { identifier = handle, password = appPassword },
            ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            return (null!, $"Authentication failed ({(int)resp.StatusCode}):\n{body}");
        }

        var session = await resp.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty session response.");
        return (session, null);
    }

    private static async Task<(string Text, List<Facet> Facets)> BuildPostAsync(string title, string url)
    {
        var (headline, tags) = await LmStudioPostGenerator.GenerateAsync(title);
        var hashtagStr = tags.Length > 0 ? "\n" + string.Join(" ", tags.Select(t => $"#{t}")) : "";

        var maxTitle = MaxPostChars - 1 - url.Length - hashtagStr.Length;
        if (headline.Length > maxTitle)
            headline = headline[..(Math.Max(maxTitle - 1, 10))] + "…";
        title = headline;

        var text = $"{title}\n{url}{hashtagStr}";
        var facets = new List<Facet>();

        // Link facet for the URL
        var urlByteStart = Encoding.UTF8.GetByteCount(title + "\n");
        var urlByteEnd   = urlByteStart + Encoding.UTF8.GetByteCount(url);
        facets.Add(new Facet
        {
            Index    = new FacetIndex { ByteStart = urlByteStart, ByteEnd = urlByteEnd },
            Features = [new LinkFeature { Uri = url }],
        });

        // Tag facets for each hashtag
        if (tags.Length > 0)
        {
            var cursor = Encoding.UTF8.GetByteCount(title + "\n" + url + "\n");
            foreach (var tag in tags)
            {
                var hashTag  = $"#{tag}";
                var tagEnd   = cursor + Encoding.UTF8.GetByteCount(hashTag);
                facets.Add(new Facet
                {
                    Index    = new FacetIndex { ByteStart = cursor, ByteEnd = tagEnd },
                    Features = [new TagFeature { Tag = tag }],
                });
                cursor = tagEnd + 1; // +1 for the space separator
            }
        }

        return (text, facets);
    }

    public void Dispose() => _http.Dispose();

    // ── AT Protocol JSON models ──────────────────────────────────────────────

    private sealed class SessionResponse
    {
        [JsonPropertyName("did")]        public string Did       { get; init; } = "";
        [JsonPropertyName("accessJwt")] public string AccessJwt { get; init; } = "";
    }

    private sealed class PostRecord
    {
        [JsonPropertyName("$type")]     public string       Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string       Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string       CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]     Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public List<Facet>  Facets    { get; init; } = [];
    }

    private sealed class Facet
    {
        [JsonPropertyName("index")]    public FacetIndex   Index    { get; init; } = new();
        [JsonPropertyName("features")] public List<object> Features { get; init; } = [];
    }

    private sealed class FacetIndex
    {
        [JsonPropertyName("byteStart")] public int ByteStart { get; init; }
        [JsonPropertyName("byteEnd")]   public int ByteEnd   { get; init; }
    }

    private sealed class LinkFeature
    {
        [JsonPropertyName("$type")] public string Type { get; init; } = "app.bsky.richtext.facet#link";
        [JsonPropertyName("uri")]   public string Uri  { get; init; } = "";
    }

    private sealed class TagFeature
    {
        [JsonPropertyName("$type")] public string Type { get; init; } = "app.bsky.richtext.facet#tag";
        [JsonPropertyName("tag")]   public string Tag  { get; init; } = "";
    }

    private sealed class UploadBlobResponse
    {
        [JsonPropertyName("blob")] public System.Text.Json.JsonElement Blob { get; init; }
    }
}
