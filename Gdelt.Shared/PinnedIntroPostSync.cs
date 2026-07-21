using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace GdeltSearchUI;

// Posts a one-time intro post on each "Live Wire" bot account linking to that
// account's own copy of the Live Wire starter pack (see StarterPackSync.cs — run
// that first so the pack exists), then pins the post to the account's profile so
// visitors immediately see a path to the other 6 bots.
//
// Pinning is a profile-record field (app.bsky.actor.profile#self.pinnedPost), not
// part of the post itself, so this reads the existing profile record, overwrites
// only pinnedPost, and writes it back — every other profile field (displayName,
// description, avatar, banner, ...) must survive untouched.
public static class PinnedIntroPostSync
{
    private const string BaseUrl = "https://bsky.social/xrpc";

    private static readonly (string Label, string Blurb, Func<(string Handle, string Password)?> Loader)[] Roster =
    [
        ("Gas Prices",   "Automated alerts for US gas prices, updated from EIA data.",
                          CredentialManager.LoadGasPriceBluesky),
        ("Debt",         "Daily automated updates on the US national debt, sourced from the Treasury Fiscal Data API.",
                          CredentialManager.LoadDebtBluesky),
        ("Energy $",     "Automated snapshots of energy futures — crude oil, natural gas, gasoline, and heating oil — from Yahoo Finance.",
                          CredentialManager.LoadYahooBluesky),
        ("NJ Birds",     "Automated highlights from the Backyard Birds of New Jersey YouTube channel.",
                          CredentialManager.LoadBirdBluesky),
        ("Quakes",       "Automated alerts for significant earthquakes worldwide, sourced from USGS.",
                          CredentialManager.LoadQuakeBluesky),
        ("Gun Violence", "Automated tracking of US gun violence news from GDELT, filtered and LLM-verified before posting.",
                          CredentialManager.LoadGunViolenceBluesky),
        ("APOD",         "NASA's Astronomy Picture of the Day, posted automatically every day.",
                          CredentialManager.LoadApodBluesky),
    ];

    // dryRun: authenticate (needed to read the starter pack) and build the exact
    // post text that would be sent, but stop before createRecord/putRecord — no
    // post, no pin, no tracker write. Purely a read-only preview.
    public static async Task PinAllAsync(Action<string> log, CancellationToken ct = default, bool dryRun = false)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        foreach (var (label, blurb, loader) in Roster)
        {
            try
            {
                await PinOneAsync(http, label, blurb, loader, log, ct, dryRun);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log($"[{label}] Failed: {ex.Message}");
            }
        }
    }

    private static async Task PinOneAsync(
        HttpClient http, string label, string blurb,
        Func<(string Handle, string Password)?> loader, Action<string> log, CancellationToken ct, bool dryRun = false)
    {
        var creds = loader();
        if (creds is null) { log($"[{label}] No credentials configured — skipped."); return; }

        var (did, jwt, err) = await AuthenticateAsync(http, creds.Value.Handle, creds.Value.Password, ct);
        if (err is not null) { log($"[{label}] Auth failed: {err}"); return; }
        var auth = new AuthenticationHeaderValue("Bearer", jwt!);

        string postUri, postCid;
        var already = PinnedIntroPostTracker.GetPosted(label);
        if (already is { } a)
        {
            (postUri, postCid) = a;
            if (dryRun)
            {
                log($"[{label}] Intro post already exists ({postUri}) — DRY RUN, would verify pin only.");
                return;
            }
            log($"[{label}] Intro post already exists ({postUri}) — verifying pin only.");
        }
        else
        {
            var packs = await ListRecordsAsync(http, auth, did!, "app.bsky.graph.starterpack", ct);
            var pack  = packs.FirstOrDefault();
            if (pack is null)
            {
                log($"[{label}] No 'Live Wire' starter pack found — run --create-starter-pack first. Skipped.");
                return;
            }

            var rkey    = RkeyFromUri(pack["uri"]!.GetValue<string>());
            var packUrl = $"https://bsky.app/starter-pack/{creds.Value.Handle}/{rkey}";
            var text    = $"{blurb}\n\nMeet the rest of the Live Wire network: {packUrl}";

            if (dryRun)
            {
                log($"[{label}] (@{creds.Value.Handle}) DRY RUN — would post:\n---\n{text}\n---\nchars: {new System.Globalization.StringInfo(text).LengthInTextElements}");
                return;
            }

            var postRecord = new JsonObject
            {
                ["$type"]     = "app.bsky.feed.post",
                ["text"]      = text,
                ["createdAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                ["langs"]     = new JsonArray { "en" },
                ["facets"]    = new JsonArray { BuildLinkFacet(text, packUrl) },
            };

            var postResp = await CreateRecordAsync(http, auth, did!, "app.bsky.feed.post", postRecord, ct);
            postUri = postResp["uri"]!.GetValue<string>();
            postCid = postResp["cid"]!.GetValue<string>();

            // Persist before attempting the pin — a failure past this point must
            // never cause a duplicate post on the next run.
            PinnedIntroPostTracker.MarkPosted(label, postUri, postCid);
            log($"[{label}] Posted intro linking to {packUrl}");
        }

        var profile = await GetRecordAsync(http, auth, did!, "app.bsky.actor.profile", "self", ct);
        if (profile is null)
        {
            log($"[{label}] No profile record found — cannot pin (intro post was still created/kept). Skipped pin.");
            return;
        }

        profile["$type"]      = "app.bsky.actor.profile";
        profile["pinnedPost"] = new JsonObject { ["uri"] = postUri, ["cid"] = postCid };

        await PutRecordAsync(http, auth, did!, "app.bsky.actor.profile", "self", profile, ct);
        log($"[{label}] Pinned intro post to profile.");
    }

    // Builds a single app.bsky.richtext.facet#link facet covering `url` within `text`.
    private static JsonObject BuildLinkFacet(string text, string url)
    {
        var idx       = text.IndexOf(url, StringComparison.Ordinal);
        var byteStart = Encoding.UTF8.GetByteCount(text[..idx]);
        var byteEnd   = byteStart + Encoding.UTF8.GetByteCount(url);
        return new JsonObject
        {
            ["index"]    = new JsonObject { ["byteStart"] = byteStart, ["byteEnd"] = byteEnd },
            ["features"] = new JsonArray { new JsonObject { ["$type"] = "app.bsky.richtext.facet#link", ["uri"] = url } },
        };
    }

    private static async Task<(string? Did, string? Jwt, string? Error)> AuthenticateAsync(
        HttpClient http, string handle, string password, CancellationToken ct)
    {
        var resp = await http.PostAsJsonAsync($"{BaseUrl}/com.atproto.server.createSession",
            new { identifier = handle, password }, ct);
        if (!resp.IsSuccessStatusCode)
            return (null, null, $"{(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync(ct)}");
        var json = await resp.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
        return (json?["did"]?.GetValue<string>(), json?["accessJwt"]?.GetValue<string>(), null);
    }

    private static async Task<List<JsonObject>> ListRecordsAsync(
        HttpClient http, AuthenticationHeaderValue auth, string repo, string collection, CancellationToken ct)
    {
        var results = new List<JsonObject>();
        string? cursor = null;
        do
        {
            var url = $"{BaseUrl}/com.atproto.repo.listRecords?repo={Uri.EscapeDataString(repo)}&collection={collection}&limit=100" +
                      (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = auth;
            var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) break;
            var json = await resp.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
            if (json?["records"] is JsonArray arr)
                foreach (var r in arr)
                    if (r is JsonObject o) results.Add(o);
            cursor = json?["cursor"]?.GetValue<string>();
        } while (cursor is not null);
        return results;
    }

    // Returns the record's `value` object (a detached clone, safe to mutate and
    // reuse as a putRecord body), or null if the record does not exist.
    private static async Task<JsonObject?> GetRecordAsync(
        HttpClient http, AuthenticationHeaderValue auth, string repo, string collection, string rkey, CancellationToken ct)
    {
        var url = $"{BaseUrl}/com.atproto.repo.getRecord?repo={Uri.EscapeDataString(repo)}&collection={collection}&rkey={rkey}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = auth;
        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null; // e.g. RecordNotFound

        var json = await resp.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
        var value = json?["value"] as JsonObject;
        if (value is null) return null;

        // Clone so the returned object has no parent — JsonNode instances can only
        // be attached under one parent at a time, and the caller re-parents this
        // into a new putRecord request body.
        return JsonNode.Parse(value.ToJsonString()) as JsonObject;
    }

    private static async Task<JsonObject> CreateRecordAsync(
        HttpClient http, AuthenticationHeaderValue auth, string repo, string collection, JsonObject record, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.createRecord")
        {
            Content = JsonContent.Create(new JsonObject { ["repo"] = repo, ["collection"] = collection, ["record"] = record }),
        };
        req.Headers.Authorization = auth;
        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"createRecord {collection} failed ({(int)resp.StatusCode}): {await resp.Content.ReadAsStringAsync(ct)}");
        return await resp.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Empty createRecord response.");
    }

    private static async Task PutRecordAsync(
        HttpClient http, AuthenticationHeaderValue auth, string repo, string collection, string rkey, JsonObject record, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.putRecord")
        {
            Content = JsonContent.Create(new JsonObject { ["repo"] = repo, ["collection"] = collection, ["rkey"] = rkey, ["record"] = record }),
        };
        req.Headers.Authorization = auth;
        var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"putRecord {collection} failed ({(int)resp.StatusCode}): {await resp.Content.ReadAsStringAsync(ct)}");
    }

    private static string RkeyFromUri(string atUri) => atUri.Split('/')[^1];
}
