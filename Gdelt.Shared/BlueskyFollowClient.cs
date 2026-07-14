using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

internal sealed class SuggestedActor
{
    [JsonPropertyName("did")]    public string Did    { get; init; } = "";
    [JsonPropertyName("handle")] public string Handle { get; init; } = "";
}

internal sealed class BlueskyFollowClient : IDisposable
{
    private const string BaseUrl = "https://bsky.social/xrpc";

    // Shared page-size ceiling for follower/following reads. Callers used to pass
    // inconsistent values (200 here, 500 there); one constant keeps follow-back and
    // the unfollow reciprocity check looking at the same window of relationships.
    public const int MaxRelationshipFetch = 1000;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<(string Did, string AccessJwt, string? Error)> AuthenticateAsync(
        string handle, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"{BaseUrl}/com.atproto.server.createSession",
            new { identifier = handle, password }, ct);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            return ("", "", $"Auth failed ({(int)resp.StatusCode}): {body}");
        }

        var session = await resp.Content.ReadFromJsonAsync<SessionResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty session response.");
        return (session.Did, session.AccessJwt, null);
    }

    public async Task<BskyProfile> GetProfileAsync(
        string did, string accessJwt, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/app.bsky.actor.getProfile?actor={Uri.EscapeDataString(did)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<BskyProfile>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty profile response.");
    }

    public async Task<BskyProfile?> GetPublicProfileAsync(
        string handleOrDid, CancellationToken ct = default, string? accessJwt = null)
    {
        var url = $"{BaseUrl}/app.bsky.actor.getProfile?actor={Uri.EscapeDataString(handleOrDid)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (accessJwt is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<BskyProfile>(cancellationToken: ct);
    }

    public async Task<List<SuggestedActor>> GetSuggestionsAsync(
        string accessJwt, int limit = 100, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/app.bsky.actor.getSuggestions?limit={Math.Min(100, limit)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<GetSuggestionsResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty suggestions response.");
        return result.Actors;
    }

    public async Task<List<SuggestedActor>> GetFollowersAsync(
        string actorDid, string accessJwt, int maxResults = 200, CancellationToken ct = default) =>
        await GetActorListPagedAsync(
            $"{BaseUrl}/app.bsky.graph.getFollowers", "followers",
            actorDid, accessJwt, maxResults, ct);

    public async Task<HashSet<string>> GetFollowDidsAsync(
        string actorDid, string accessJwt, int maxResults = 200, CancellationToken ct = default)
    {
        var actors = await GetActorListPagedAsync(
            $"{BaseUrl}/app.bsky.graph.getFollows", "follows",
            actorDid, accessJwt, maxResults, ct);
        return new HashSet<string>(actors.Select(a => a.Did), StringComparer.OrdinalIgnoreCase);
    }

    // Returns subjectDid → rkey for all follow records in the repo.
    public async Task<Dictionary<string, string>> GetFollowRecordRkeysAsync(
        string repoDid, string accessJwt, CancellationToken ct = default)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? cursor = null;

        while (true)
        {
            var url = $"{BaseUrl}/com.atproto.repo.listRecords" +
                      $"?repo={Uri.EscapeDataString(repoDid)}&collection=app.bsky.graph.follow&limit=100";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            cursor = doc.TryGetProperty("cursor", out var c) ? c.GetString() : null;

            if (!doc.TryGetProperty("records", out var records)) break;
            foreach (var record in records.EnumerateArray())
            {
                // uri format: at://did/app.bsky.graph.follow/{rkey}
                var uri    = record.TryGetProperty("uri",   out var u) ? u.GetString() ?? "" : "";
                var rkey   = uri.Split('/').LastOrDefault() ?? "";
                var subject = record.TryGetProperty("value", out var v) &&
                              v.TryGetProperty("subject",    out var s)
                              ? s.GetString() ?? "" : "";

                if (rkey.Length > 0 && subject.Length > 0)
                    result[subject] = rkey;
            }

            if (string.IsNullOrEmpty(cursor)) break;
        }

        return result;
    }

    public async Task<(bool Ok, string? Error)> UnfollowAsync(
        string repoDid, string rkey, string accessJwt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.deleteRecord")
        {
            Content = JsonContent.Create(new
            {
                repo       = repoDid,
                collection = "app.bsky.graph.follow",
                rkey,
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode) return (true, null);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return (false, $"Unfollow failed ({(int)resp.StatusCode}): {body}");
    }

    private async Task<List<SuggestedActor>> GetActorListPagedAsync(
        string endpoint, string fieldName, string actorDid,
        string accessJwt, int maxResults, CancellationToken ct)
    {
        var all    = new List<SuggestedActor>();
        string? cursor = null;

        while (all.Count < maxResults)
        {
            var url = $"{endpoint}?actor={Uri.EscapeDataString(actorDid)}&limit=100";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            cursor = doc.TryGetProperty("cursor", out var c) ? c.GetString() : null;

            if (!doc.TryGetProperty(fieldName, out var arr)) break;
            foreach (var item in arr.EnumerateArray())
            {
                var did    = item.TryGetProperty("did",    out var d) ? d.GetString() ?? "" : "";
                var handle = item.TryGetProperty("handle", out var h) ? h.GetString() ?? "" : "";
                if (did.Length > 0)
                    all.Add(new SuggestedActor { Did = did, Handle = handle });
            }

            if (string.IsNullOrEmpty(cursor)) break;
        }

        return all.Take(maxResults).ToList();
    }

    public async Task<List<BskyPost>> SearchPostsAsync(
        string query, string accessJwt, int limit = 50, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/app.bsky.feed.searchPosts" +
                  $"?q={Uri.EscapeDataString(query)}&limit={Math.Min(100, limit)}&sort=top";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadFromJsonAsync<BskySearchPostsResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty search response.");
        return result.Posts;
    }

    public async Task<(bool Ok, bool AlreadyFollowing, string? Error)> FollowAsync(
        string repoDid, string subjectDid, string accessJwt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(new
            {
                repo       = repoDid,
                collection = "app.bsky.graph.follow",
                record     = new FollowRecord { Subject = subjectDid },
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode) return (true, false, null);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if ((int)resp.StatusCode == 400 &&
            (body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("InvalidRequest", StringComparison.OrdinalIgnoreCase)))
            return (false, true, null);
        return (false, false, $"Follow failed ({(int)resp.StatusCode}): {body}");
    }

    public async Task<(bool Ok, bool AlreadyLiked, string? Error)> LikeAsync(
        string repoDid, string postUri, string postCid, string accessJwt, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(new
            {
                repo       = repoDid,
                collection = "app.bsky.feed.like",
                record     = new LikeRecord
                {
                    Subject   = new LikeSubject { Uri = postUri, Cid = postCid },
                    CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                },
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessJwt);
        using var resp = await _http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode) return (true, false, null);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if ((int)resp.StatusCode == 400 &&
            (body.Contains("already", StringComparison.OrdinalIgnoreCase) ||
             body.Contains("InvalidRequest", StringComparison.OrdinalIgnoreCase)))
            return (false, true, null);
        return (false, false, $"Like failed ({(int)resp.StatusCode}): {body}");
    }

    public void Dispose() => _http.Dispose();

    private sealed class SessionResponse
    {
        [JsonPropertyName("did")]       public string Did       { get; init; } = "";
        [JsonPropertyName("accessJwt")] public string AccessJwt { get; init; } = "";
    }

    private sealed class GetSuggestionsResponse
    {
        [JsonPropertyName("actors")] public List<SuggestedActor> Actors { get; init; } = [];
    }

    private sealed class FollowRecord
    {
        [JsonPropertyName("$type")]     public string Type      { get; init; } = "app.bsky.graph.follow";
        [JsonPropertyName("subject")]   public string Subject   { get; init; } = "";
        [JsonPropertyName("createdAt")] public string CreatedAt { get; init; } =
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    private sealed class LikeRecord
    {
        [JsonPropertyName("$type")]     public string      Type      { get; init; } = "app.bsky.feed.like";
        [JsonPropertyName("subject")]   public LikeSubject Subject   { get; init; } = new();
        [JsonPropertyName("createdAt")] public string      CreatedAt { get; init; } = "";
    }

    private sealed class LikeSubject
    {
        [JsonPropertyName("uri")] public string Uri { get; init; } = "";
        [JsonPropertyName("cid")] public string Cid { get; init; } = "";
    }
}
