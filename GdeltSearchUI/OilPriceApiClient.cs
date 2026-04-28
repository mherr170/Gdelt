using System.Text.Json;

namespace GdeltSearchUI;

internal sealed class OilPriceApiClient : IDisposable
{
    private const string Base = "https://api.oilpriceapi.com/v1";

    // Ordered: crude benchmarks first, then refined products
    public static readonly (string Code, string DisplayName, string Unit)[] Catalog =
    [
        ("BRENT_CRUDE_USD",   "Brent Crude",    "$/bbl"),
        ("WTI_USD",           "WTI Crude",      "$/bbl"),
        ("NATURAL_GAS_USD",   "Natural Gas",    "$/MMBtu"),
        ("GASOLINE_RBOB_USD", "RBOB Gasoline",  "$/gal"),
        ("HEATING_OIL_USD",   "Heating Oil",    "$/gal"),
        ("ULSD_DIESEL_USD",   "ULSD Diesel",    "$/gal"),
    ];

    private static readonly Dictionary<string, (string Code, string DisplayName, string Unit)>
        _lookup = Catalog.ToDictionary(c => c.Code);

    private readonly HttpClient _http;

    public OilPriceApiClient(string apiKey)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("Authorization", $"Token {apiKey}");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    // Fetches all catalog codes in a single request.
    // The API returns an array when multiple codes are requested,
    // or an object when a single code is requested — we handle both.
    public async Task<IReadOnlyList<OilPriceEntry>> GetLatestAsync()
    {
        var codes = string.Join(",", Catalog.Select(c => c.Code));
        var json  = await _http.GetStringAsync($"{Base}/prices/latest?by_code={codes}");
        return Parse(json);
    }

    private static IReadOnlyList<OilPriceEntry> Parse(string json)
    {
        var result = new List<OilPriceEntry>(Catalog.Length);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("status", out var status) &&
            status.GetString() != "success") return result;

        if (!root.TryGetProperty("data", out var data)) return result;

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in data.EnumerateArray())
                TryAdd(elem, result);
        }
        else if (data.ValueKind == JsonValueKind.Object)
        {
            TryAdd(data, result);
        }

        // Preserve catalog display order
        var order = Catalog.Select((c, i) => (c.Code, i)).ToDictionary(x => x.Code, x => x.i);
        result.Sort((a, b) =>
            order.TryGetValue(a.Code, out var ia) & order.TryGetValue(b.Code, out var ib)
                ? ia.CompareTo(ib) : 0);

        return result;
    }

    private static void TryAdd(JsonElement elem, List<OilPriceEntry> result)
    {
        if (!elem.TryGetProperty("code", out var codeProp)) return;
        var code = codeProp.GetString() ?? "";
        if (!_lookup.TryGetValue(code, out var meta)) return;

        var price = elem.TryGetProperty("price", out var p) ? p.GetDouble() : 0;
        DateTimeOffset updatedAt = default;
        if (elem.TryGetProperty("created_at", out var ts))
            DateTimeOffset.TryParse(ts.GetString(), out updatedAt);

        result.Add(new OilPriceEntry(code, meta.DisplayName, meta.Unit, price, updatedAt));
    }

    public void Dispose() => _http.Dispose();
}
