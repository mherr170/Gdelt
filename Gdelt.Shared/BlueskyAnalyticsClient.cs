using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GdeltSearchUI;

internal sealed class BlueskyAnalyticsClient : IDisposable
{
    private const string Base = "https://bsky.social/xrpc";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _accessJwt;

    public async Task AuthenticateAsync(string handle, string password, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync(
            $"{Base}/com.atproto.server.createSession",
            new { identifier = handle, password }, ct);
        resp.EnsureSuccessStatusCode();
        var session = await resp.Content.ReadFromJsonAsync<BskySession>(ct)
            ?? throw new InvalidOperationException("Empty session response.");
        _accessJwt = session.AccessJwt;
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (_accessJwt is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessJwt);

        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new InvalidOperationException("Empty response.");
    }

    public async Task<BskyProfile> GetProfileAsync(string handle, CancellationToken ct = default)
    {
        var url = $"{Base}/app.bsky.actor.getProfile?actor={Uri.EscapeDataString(handle)}";
        return await GetJsonAsync<BskyProfile>(url, ct);
    }

    public async Task<List<BskyProfile>> GetProfilesAsync(
        IEnumerable<string> handles, CancellationToken ct = default)
    {
        var list = handles.Take(25).ToList();
        if (list.Count == 0) return [];
        var qs  = string.Join("&", list.Select(h => $"actors={Uri.EscapeDataString(h)}"));
        var url = $"{Base}/app.bsky.actor.getProfiles?{qs}";
        var resp = await GetJsonAsync<BskyGetProfilesResponse>(url, ct);
        return resp.Profiles;
    }

    // Fetches up to maxPosts original posts (reposts excluded) from the author's feed.
    public async Task<List<BskyPost>> GetAuthorPostsAsync(
        string handle, int maxPosts = 100, bool includeReplies = true, CancellationToken ct = default)
    {
        var all    = new List<BskyPost>();
        string? cursor = null;

        while (all.Count < maxPosts)
        {
            var pageLimit = Math.Min(100, maxPosts - all.Count + 20);
            var url = $"{Base}/app.bsky.feed.getAuthorFeed" +
                      $"?actor={Uri.EscapeDataString(handle)}&limit={pageLimit}";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            var resp = await GetJsonAsync<BskyAuthorFeedResponse>(url, ct);
            if (resp.Feed.Count == 0) break;

            var originals = resp.Feed
                .Where(f => !f.IsRepost)
                .Where(f => includeReplies || !f.IsReply)
                .Select(f => f.Post);

            all.AddRange(originals);
            cursor = resp.Cursor;
            if (string.IsNullOrEmpty(cursor)) break;
        }

        return [.. all.Take(maxPosts)];
    }

    // Searches across all of Bluesky, sorted by top engagement.
    public async Task<List<BskyPost>> SearchPostsAsync(
        string query, int limit = 100, CancellationToken ct = default)
    {
        var all    = new List<BskyPost>();
        string? cursor = null;

        while (all.Count < limit)
        {
            var pageLimit = Math.Min(100, limit - all.Count);
            var url = $"{Base}/app.bsky.feed.searchPosts" +
                      $"?q={Uri.EscapeDataString(query)}&limit={pageLimit}&sort=top";
            if (cursor is not null) url += $"&cursor={Uri.EscapeDataString(cursor)}";

            var resp = await GetJsonAsync<BskySearchPostsResponse>(url, ct);
            if (resp.Posts.Count == 0) break;

            all.AddRange(resp.Posts);
            cursor = resp.Cursor;
            if (string.IsNullOrEmpty(cursor)) break;
        }

        return [.. all.Take(limit)];
    }

    public async Task<List<BskyAuthor>> SearchActorsAsync(
        string query, int limit = 50, CancellationToken ct = default)
    {
        var url = $"{Base}/app.bsky.actor.searchActors" +
                  $"?q={Uri.EscapeDataString(query)}&limit={Math.Min(100, limit)}";
        var resp = await GetJsonAsync<BskySearchActorsResponse>(url, ct);
        return resp.Actors;
    }

    public void Dispose() { } // _http is static/shared — not disposed per-instance
}
