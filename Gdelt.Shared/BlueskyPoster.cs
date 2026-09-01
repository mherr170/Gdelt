using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
        using var postResp = await SendWithRetryAsync(() =>
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
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
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
            return r;
        }, ct);
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

        text = TruncateToFit(text);

        // 1. Upload image as a blob.
        using var blobReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.uploadBlob")
        {
            Content = new ByteArrayContent(imageBytes),
        };
        var imageMime = imageBytes.Length >= 3 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8 && imageBytes[2] == 0xFF
            ? "image/jpeg" : "image/png";
        blobReq.Content.Headers.ContentType = new MediaTypeHeaderValue(imageMime);
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
        var record = new PostRecordWithImage
        {
            Text      = text,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Langs     = ["en"],
            Facets    = BuildHashtagFacets(text),
            Embed     = new ImageEmbed
            {
                Images = [new ImageItem { Alt = altText, Image = blobJson.Blob }],
            },
        };

        using var postResp = await SendWithRetryAsync(() =>
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
            {
                Content = JsonContent.Create(new
                {
                    repo       = session.Did,
                    collection = "app.bsky.feed.post",
                    record,
                }),
            };
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
            return r;
        }, ct);
        if (!postResp.IsSuccessStatusCode)
        {
            var body = await postResp.Content.ReadAsStringAsync(ct);
            return (false, $"Post failed ({(int)postResp.StatusCode}):\n{body}");
        }

        return (true, null);
    }

    // Posts pre-built text together with an external link card (app.bsky.embed.external).
    // Bluesky does not generate link previews server-side, so the thumbnail card must be
    // built here: the thumb image is uploaded as a blob and attached to the embed.
    // If the thumb download/upload fails, the card is still posted without an image.
    public async Task<(bool Ok, string? Error)> PostExternalLinkAsync(
        string handle, string appPassword, string text,
        string linkUri, string cardTitle, string cardDescription,
        byte[]? thumbBytes, CancellationToken ct)
    {
        var (session, authError) = await AuthenticateAsync(handle, appPassword, ct);
        if (authError is not null) return (false, authError);

        text = TruncateToFit(text);

        // 1. Upload thumbnail as a blob (best-effort — card still posts without it).
        System.Text.Json.JsonElement? thumbBlob = null;
        if (thumbBytes is { Length: > 0 })
        {
            using var blobReq = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.uploadBlob")
            {
                Content = new ByteArrayContent(thumbBytes),
            };
            blobReq.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            blobReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);

            var blobResp = await _http.SendAsync(blobReq, ct);
            if (blobResp.IsSuccessStatusCode)
            {
                var blobJson = await blobResp.Content.ReadFromJsonAsync<UploadBlobResponse>(cancellationToken: ct);
                if (blobJson is not null) thumbBlob = blobJson.Blob;
            }
            else
            {
                var errBody = await blobResp.Content.ReadAsStringAsync(ct);
                PostLogger.Warn("bluesky", $"Thumbnail upload failed ({(int)blobResp.StatusCode}) — posting card without image: {errBody[..Math.Min(errBody.Length, 200)]}");
            }
        }

        // 2. Create record with external embed.
        var record = new PostRecordWithExternal
        {
            Text      = text,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Langs     = ["en"],
            Facets    = BuildHashtagFacets(text),
            Embed     = new ExternalEmbed
            {
                External = new ExternalInfo
                {
                    Uri         = linkUri,
                    Title       = cardTitle,
                    Description = cardDescription,
                    Thumb       = thumbBlob,
                },
            },
        };

        using var postResp = await SendWithRetryAsync(() =>
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
            {
                Content = JsonContent.Create(new
                {
                    repo       = session.Did,
                    collection = "app.bsky.feed.post",
                    record,
                }),
            };
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
            return r;
        }, ct);
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

        text = TruncateToFit(text);

        using var postResp = await SendWithRetryAsync(() =>
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
            {
                Content = JsonContent.Create(new
                {
                    repo = session.Did,
                    collection = "app.bsky.feed.post",
                    record = new HashtagPostRecord
                    {
                        Text = text,
                        CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        Langs = ["en"],
                        Facets = BuildHashtagFacets(text),
                    },
                }),
            };
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
            return r;
        }, ct);
        if (!postResp.IsSuccessStatusCode)
        {
            var body = await postResp.Content.ReadAsStringAsync(ct);
            return (false, $"Post failed ({(int)postResp.StatusCode}):\n{body}");
        }

        return (true, null);
    }

    // Posts pre-built text with a clickable link facet for linkUrl (which must appear verbatim in text)
    // plus hashtag facets — used when no thumbnail card is available for video posts.
    public async Task<(bool Ok, string? Error)> PostTextWithLinkAsync(
        string handle, string appPassword, string text, string linkUrl, CancellationToken ct)
    {
        var (session, authError) = await AuthenticateAsync(handle, appPassword, ct);
        if (authError is not null) return (false, authError);

        text = TruncateToFit(text);

        var facets = BuildMixedFacetsJson(text, linkUrl);

        using var postResp = await SendWithRetryAsync(() =>
        {
            var r = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
            {
                Content = JsonContent.Create(new
                {
                    repo       = session.Did,
                    collection = "app.bsky.feed.post",
                    record     = new PostRecordWithJsonFacets
                    {
                        Text      = text,
                        CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                        Langs     = ["en"],
                        Facets    = facets,
                    },
                }),
            };
            r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
            return r;
        }, ct);
        if (!postResp.IsSuccessStatusCode)
        {
            var body = await postResp.Content.ReadAsStringAsync(ct);
            return (false, $"Post failed ({(int)postResp.StatusCode}):\n{body}");
        }

        return (true, null);
    }

    // Posts `parts` as a self-reply thread: parts[0] is the root post, each later
    // part replies to the one before it. Every part is truncated to fit on its
    // own. Returns Ok only if all parts posted; a mid-thread failure leaves the
    // earlier parts up (the caller does not advance, so it will re-post the whole
    // verse as a fresh thread next hour — a rare, acceptable orphan).
    public async Task<(bool Ok, string? Error)> PostThreadAsync(
        string handle, string appPassword, IReadOnlyList<string> parts, CancellationToken ct)
    {
        if (parts.Count == 0) return (false, "No content to post.");
        if (parts.Count == 1) return await PostTextAsync(handle, appPassword, parts[0], ct);

        var (session, authError) = await AuthenticateAsync(handle, appPassword, ct);
        if (authError is not null) return (false, authError);

        PostStrongRef? root   = null;
        PostStrongRef? parent = null;

        for (int i = 0; i < parts.Count; i++)
        {
            var text  = TruncateToFit(parts[i]);
            var reply = parent is null ? null : new ReplyRef { Root = root!, Parent = parent };

            using var postResp = await SendWithRetryAsync(() =>
            {
                var r = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
                {
                    Content = JsonContent.Create(new
                    {
                        repo       = session.Did,
                        collection = "app.bsky.feed.post",
                        record     = new ThreadPostRecord
                        {
                            Text      = text,
                            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                            Langs     = ["en"],
                            Facets    = BuildHashtagFacets(text),
                            Reply     = reply,
                        },
                    }),
                };
                r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessJwt);
                return r;
            }, ct);

            if (!postResp.IsSuccessStatusCode)
            {
                var body = await postResp.Content.ReadAsStringAsync(ct);
                return (false, $"Thread part {i + 1}/{parts.Count} failed ({(int)postResp.StatusCode}):\n{body}");
            }

            var created = await postResp.Content.ReadFromJsonAsync<CreateRecordResponse>(cancellationToken: ct);
            if (created is null || string.IsNullOrEmpty(created.Uri) || string.IsNullOrEmpty(created.Cid))
                return (false, $"Thread part {i + 1}: empty createRecord response.");

            parent = new PostStrongRef { Uri = created.Uri, Cid = created.Cid };
            root ??= parent;
        }

        return (true, null);
    }

    // Builds a JsonArray of facets (link + hashtags) without relying on STJ runtime-type
    // resolution, which does not serialize List<object> feature elements correctly.
    private static JsonArray BuildMixedFacetsJson(string text, string? linkUrl)
    {
        var arr = new JsonArray();

        if (linkUrl is not null)
        {
            var idx = text.IndexOf(linkUrl, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var byteStart = Encoding.UTF8.GetByteCount(text[..idx]);
                var byteEnd   = byteStart + Encoding.UTF8.GetByteCount(linkUrl);
                arr.Add(new JsonObject
                {
                    ["index"]    = new JsonObject { ["byteStart"] = byteStart, ["byteEnd"] = byteEnd },
                    ["features"] = new JsonArray { new JsonObject { ["$type"] = "app.bsky.richtext.facet#link", ["uri"] = linkUrl } },
                });
            }
        }

        var i = 0;
        while (i < text.Length)
        {
            if (text[i] != '#') { i++; continue; }
            if (i > 0 && !char.IsWhiteSpace(text[i - 1])) { i++; continue; }
            var start = i + 1;
            var end   = start;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            if (end == start) { i++; continue; }
            var tag       = text[start..end];
            var byteStart = Encoding.UTF8.GetByteCount(text[..i]);
            var byteEnd   = byteStart + Encoding.UTF8.GetByteCount(text[i..end]);
            arr.Add(new JsonObject
            {
                ["index"]    = new JsonObject { ["byteStart"] = byteStart, ["byteEnd"] = byteEnd },
                ["features"] = new JsonArray { new JsonObject { ["$type"] = "app.bsky.richtext.facet#tag", ["tag"] = tag } },
            });
            i = end;
        }

        return arr;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            using var req = requestFactory();
            var response = await _http.SendAsync(req, ct);

            var status = (int)response.StatusCode;
            bool retryable = (status == 429 || status == 502) && attempt < maxAttempts - 1;
            if (!retryable) return response;

            TimeSpan delay = status == 429 && response.Headers.RetryAfter?.Delta.HasValue == true
                ? response.Headers.RetryAfter.Delta.Value
                : TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)); // 2s, 4s

            response.Dispose();
            PostLogger.Warn("bluesky", $"createRecord returned {status} — retrying in {delay.TotalSeconds:F0}s (attempt {attempt + 1}/{maxAttempts})");
            await Task.Delay(delay, ct);
        }
        throw new InvalidOperationException("Unreachable");
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

    private static readonly Regex TrailingHashtagLine = new(@"\n\n#\S+(?:\s#\S+)*$", RegexOptions.Compiled);

    // Truncates text to MaxPostChars. Widget post-text builders append hashtags as a
    // trailing "\n\n#tag1 #tag2" line, so naively cutting from the end silently drops
    // them on long posts — this pulls that line off first, truncates the remaining
    // body, then reattaches it so hashtags always survive.
    private static string TruncateToFit(string text)
    {
        var si = new System.Globalization.StringInfo(text);
        if (si.LengthInTextElements <= MaxPostChars) return text;

        var tagMatch = TrailingHashtagLine.Match(text);
        var tagLine  = tagMatch.Success ? tagMatch.Value : "";
        var body     = tagLine.Length > 0 ? text[..^tagLine.Length] : text;

        var tagLineLen = new System.Globalization.StringInfo(tagLine).LengthInTextElements;
        var budget     = Math.Max(0, MaxPostChars - tagLineLen - 1); // -1 for ellipsis

        var bodyInfo       = new System.Globalization.StringInfo(body);
        var truncatedBody  = bodyInfo.LengthInTextElements > budget
            ? bodyInfo.SubstringByTextElements(0, budget) + "…"
            : body;

        return truncatedBody + tagLine;
    }

    private static List<TagFacet> BuildHashtagFacets(string text)
    {
        var facets = new List<TagFacet>();
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] != '#') { i++; continue; }
            // Must be at start or preceded by whitespace
            if (i > 0 && !char.IsWhiteSpace(text[i - 1])) { i++; continue; }
            var start = i + 1;
            var end = start;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            if (end == start) { i++; continue; } // bare #
            var tag = text[start..end];
            var byteStart = Encoding.UTF8.GetByteCount(text[..i]);
            var byteEnd   = byteStart + Encoding.UTF8.GetByteCount(text[i..end]);
            facets.Add(new TagFacet
            {
                Index    = new FacetIndex { ByteStart = byteStart, ByteEnd = byteEnd },
                Features = [new TagFeature { Tag = tag }],
            });
            i = end;
        }
        return facets;
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

    // Used by PostAsync (URL/article posts) — features stay as List<object> since BuildPostAsync
    // mixes LinkFeature and TagFeature and these posts are already working.
    private sealed class PostRecord
    {
        [JsonPropertyName("$type")]     public string      Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string      Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string      CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]    Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public List<Facet> Facets    { get; init; } = [];
    }

    // Used by PostTextAsync (text-only hashtag posts) — fully typed so STJ always
    // emits the correct feature properties without relying on runtime type resolution.
    private sealed class HashtagPostRecord
    {
        [JsonPropertyName("$type")]     public string          Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string          Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string          CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]        Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public List<TagFacet>  Facets    { get; init; } = [];
    }

    // Typed facet for hashtag features — avoids List<object> polymorphism issues with STJ.
    private sealed class TagFacet
    {
        [JsonPropertyName("index")]    public FacetIndex   Index    { get; init; } = new();
        [JsonPropertyName("features")] public TagFeature[] Features { get; init; } = [];
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

    private sealed class PostRecordWithImage
    {
        [JsonPropertyName("$type")]     public string          Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string          Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string          CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]        Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public List<TagFacet>  Facets    { get; init; } = [];
        [JsonPropertyName("embed")]     public ImageEmbed      Embed     { get; init; } = null!;
    }

    private sealed class ImageEmbed
    {
        [JsonPropertyName("$type")]  public string      Type   { get; init; } = "app.bsky.embed.images";
        [JsonPropertyName("images")] public ImageItem[] Images { get; init; } = [];
    }

    private sealed class ImageItem
    {
        [JsonPropertyName("alt")]   public string                        Alt   { get; init; } = "";
        [JsonPropertyName("image")] public System.Text.Json.JsonElement  Image { get; init; }
    }

    private sealed class PostRecordWithJsonFacets
    {
        [JsonPropertyName("$type")]     public string    Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string    Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string    CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]  Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public JsonArray Facets    { get; init; } = [];
    }

    private sealed class PostRecordWithExternal
    {
        [JsonPropertyName("$type")]     public string         Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string         Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string         CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]       Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public List<TagFacet> Facets    { get; init; } = [];
        [JsonPropertyName("embed")]     public ExternalEmbed  Embed     { get; init; } = null!;
    }

    private sealed class ExternalEmbed
    {
        [JsonPropertyName("$type")]    public string       Type     { get; init; } = "app.bsky.embed.external";
        [JsonPropertyName("external")] public ExternalInfo External { get; init; } = new();
    }

    private sealed class ExternalInfo
    {
        [JsonPropertyName("uri")]         public string Uri         { get; init; } = "";
        [JsonPropertyName("title")]       public string Title       { get; init; } = "";
        [JsonPropertyName("description")] public string Description { get; init; } = "";

        // Optional blob — omitted entirely when the thumbnail upload failed.
        [JsonPropertyName("thumb")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public System.Text.Json.JsonElement? Thumb { get; init; }
    }

    private sealed class UploadBlobResponse
    {
        [JsonPropertyName("blob")] public System.Text.Json.JsonElement Blob { get; init; }
    }

    // ── Thread (self-reply) models ───────────────────────────────────────────

    private sealed class ThreadPostRecord
    {
        [JsonPropertyName("$type")]     public string         Type      { get; init; } = "app.bsky.feed.post";
        [JsonPropertyName("text")]      public string         Text      { get; init; } = "";
        [JsonPropertyName("createdAt")] public string         CreatedAt { get; init; } = "";
        [JsonPropertyName("langs")]     public string[]       Langs     { get; init; } = [];
        [JsonPropertyName("facets")]    public List<TagFacet> Facets    { get; init; } = [];

        [JsonPropertyName("reply")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReplyRef? Reply { get; init; }
    }

    private sealed class ReplyRef
    {
        [JsonPropertyName("root")]   public PostStrongRef Root   { get; init; } = new();
        [JsonPropertyName("parent")] public PostStrongRef Parent { get; init; } = new();
    }

    private sealed class PostStrongRef
    {
        [JsonPropertyName("uri")] public string Uri { get; init; } = "";
        [JsonPropertyName("cid")] public string Cid { get; init; } = "";
    }

    private sealed class CreateRecordResponse
    {
        [JsonPropertyName("uri")] public string Uri { get; init; } = "";
        [JsonPropertyName("cid")] public string Cid { get; init; } = "";
    }
}
