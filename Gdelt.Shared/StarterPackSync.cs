using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace GdeltSearchUI;

// Creates/updates an identical Bluesky starter pack on every bot account in a
// network, so that discovering any one bot surfaces a path to all the others.
// A starter pack is two records: an app.bsky.graph.list (purpose=referencelist)
// holding one app.bsky.graph.listitem per member, plus an app.bsky.graph.starterpack
// record pointing at that list's AT-URI. Each account creates/owns its own copy.
public static class StarterPackSync
{
    private const string BaseUrl = "https://bsky.social/xrpc";

    // Syncs the starter pack for every network in BotNetworks.All that has at
    // least one account. Networks are independent — one failing does not stop
    // the others.
    public static async Task SyncAllAsync(Action<string> log, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        foreach (var network in BotNetworks.All)
        {
            if (network.Accounts.Count == 0)
            {
                log($"=== {network.Name}: no accounts configured — skipped. ===");
                continue;
            }

            log($"=== {network.Name} ===");
            await SyncNetworkAsync(http, network, log, ct);
        }
    }

    private static async Task SyncNetworkAsync(HttpClient http, BotNetwork network, Action<string> log, CancellationToken ct)
    {
        var accounts = new List<(string Label, string Did, string Jwt)>();
        foreach (var acct in network.Accounts)
        {
            var creds = acct.LoadCreds();
            if (creds is null) { log($"[{acct.Label}] No credentials configured — skipped."); continue; }

            var (did, jwt, err) = await AuthenticateAsync(http, creds.Value.Handle, creds.Value.Password, ct);
            if (err is not null) { log($"[{acct.Label}] Auth failed: {err}"); continue; }

            accounts.Add((acct.Label, did!, jwt!));
        }

        if (accounts.Count == 0) { log($"[{network.Name}] No accounts available — aborting."); return; }
        if (accounts.Count < network.Accounts.Count)
            log($"[{network.Name}] Proceeding with {accounts.Count}/{network.Accounts.Count} accounts — the pack will only list who's available.");

        var memberDids = accounts.Select(a => a.Did).ToList();

        foreach (var acct in accounts)
        {
            try
            {
                await SyncOneAsync(http, network.Name, network.Description, acct.Label, acct.Did, acct.Jwt, memberDids, log, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log($"[{acct.Label}] Failed: {ex.Message}");
            }
        }
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

    private static async Task SyncOneAsync(
        HttpClient http, string packName, string packDescription,
        string label, string did, string jwt,
        List<string> memberDids, Action<string> log, CancellationToken ct)
    {
        var auth = new AuthenticationHeaderValue("Bearer", jwt);
        var createdAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        var existingPacks = await ListRecordsAsync(http, auth, did, "app.bsky.graph.starterpack", ct);
        var existing = existingPacks.FirstOrDefault();

        string listUri;
        if (existing is not null)
        {
            var spRkey = RkeyFromUri(existing["uri"]!.GetValue<string>());
            listUri = existing["value"]!["list"]!.GetValue<string>();
            var listRkey = RkeyFromUri(listUri);
            log($"[{label}] Found existing starter pack — updating its membership in place.");

            // Wipe old membership; we rebuild it from the current roster below.
            var oldItems = await ListRecordsAsync(http, auth, did, "app.bsky.graph.listitem", ct);
            foreach (var item in oldItems)
            {
                if (item["value"]?["list"]?.GetValue<string>() != listUri) continue;
                await DeleteRecordAsync(http, auth, did, "app.bsky.graph.listitem", RkeyFromUri(item["uri"]!.GetValue<string>()), ct);
            }

            await PutRecordAsync(http, auth, did, "app.bsky.graph.list", listRkey, new JsonObject
            {
                ["$type"]       = "app.bsky.graph.list",
                ["purpose"]     = "app.bsky.graph.defs#referencelist",
                ["name"]        = packName,
                ["description"] = packDescription,
                ["createdAt"]   = createdAt,
            }, ct);

            await PutRecordAsync(http, auth, did, "app.bsky.graph.starterpack", spRkey, new JsonObject
            {
                ["$type"]       = "app.bsky.graph.starterpack",
                ["name"]        = packName,
                ["description"] = packDescription,
                ["list"]        = listUri,
                ["createdAt"]   = createdAt,
            }, ct);
        }
        else
        {
            log($"[{label}] No existing starter pack — creating new.");
            var listResp = await CreateRecordAsync(http, auth, did, "app.bsky.graph.list", new JsonObject
            {
                ["$type"]       = "app.bsky.graph.list",
                ["purpose"]     = "app.bsky.graph.defs#referencelist",
                ["name"]        = packName,
                ["description"] = packDescription,
                ["createdAt"]   = createdAt,
            }, ct);
            listUri = listResp["uri"]!.GetValue<string>();

            await CreateRecordAsync(http, auth, did, "app.bsky.graph.starterpack", new JsonObject
            {
                ["$type"]       = "app.bsky.graph.starterpack",
                ["name"]        = packName,
                ["description"] = packDescription,
                ["list"]        = listUri,
                ["createdAt"]   = createdAt,
            }, ct);
        }

        foreach (var memberDid in memberDids)
        {
            await CreateRecordAsync(http, auth, did, "app.bsky.graph.listitem", new JsonObject
            {
                ["$type"]     = "app.bsky.graph.listitem",
                ["subject"]   = memberDid,
                ["list"]      = listUri,
                ["createdAt"] = createdAt,
            }, ct);
            await Task.Delay(150, ct); // stay well clear of createRecord rate limits
        }

        log($"[{label}] Starter pack ready with {memberDids.Count} member(s).");
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

    private static async Task DeleteRecordAsync(
        HttpClient http, AuthenticationHeaderValue auth, string repo, string collection, string rkey, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/com.atproto.repo.deleteRecord")
        {
            Content = JsonContent.Create(new JsonObject { ["repo"] = repo, ["collection"] = collection, ["rkey"] = rkey }),
        };
        req.Headers.Authorization = auth;
        await http.SendAsync(req, ct); // best-effort cleanup
    }

    private static string RkeyFromUri(string atUri) => atUri.Split('/')[^1];
}
