using System.Text.Json;

namespace GdeltSearchUI;

// Fetches near-real-time energy futures prices from Yahoo Finance.
// No API key required. Prices are ~15 minutes delayed.
// Each symbol is fetched in parallel; per-symbol failures are non-fatal.
internal sealed class YahooFinanceApiClient : IDisposable
{
    private const string Base = "https://query1.finance.yahoo.com/v8/finance/chart";

    public static readonly (string Symbol, string Code, string DisplayName, string Unit)[] Catalog =
    [
        ("BZ=F", "BRENT_CRUDE",   "Brent Crude",   "$/bbl"),
        ("CL=F", "WTI_CRUDE",     "WTI Crude",     "$/bbl"),
        ("NG=F", "NATURAL_GAS",   "Natural Gas",   "$/MMBtu"),
        ("RB=F", "RBOB_GASOLINE", "RBOB Gasoline", "$/gal"),
        ("HO=F", "HEATING_OIL",   "Heating Oil",   "$/gal"),
    ];

    private readonly HttpClient _http;

    public YahooFinanceApiClient()
    {
        _http = new HttpClient();
        // A real UA avoids bot-detection refusals from Yahoo's CDN
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    // All symbols fetched in parallel; any that fail are silently omitted.
    public async Task<IReadOnlyList<OilPriceEntry>> GetLatestAsync()
    {
        var tasks   = Catalog.Select(FetchOneAsync).ToArray();
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<OilPriceEntry>().ToList();
    }

    private async Task<OilPriceEntry?> FetchOneAsync(
        (string Symbol, string Code, string DisplayName, string Unit) meta)
    {
        try
        {
            var url  = $"{Base}/{Uri.EscapeDataString(meta.Symbol)}?interval=1d&range=5d";
            var json = await _http.GetStringAsync(url);
            return Parse(json, meta);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Yahoo Finance [{meta.Symbol}]: {ex.Message}");
            return null;
        }
    }

    private static OilPriceEntry? Parse(
        string json,
        (string Symbol, string Code, string DisplayName, string Unit) meta)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("chart", out var chart)) return null;
        if (!chart.TryGetProperty("result", out var results)) return null;
        if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;

        var m = results[0].GetProperty("meta");

        var price = m.GetProperty("regularMarketPrice").GetDouble();
        var prev  = m.TryGetProperty("previousClose", out var pc) && pc.GetDouble() != 0
            ? (double?)pc.GetDouble()
            : null;
        var updatedAt = m.TryGetProperty("regularMarketTime", out var ts)
            ? DateTimeOffset.FromUnixTimeSeconds(ts.GetInt64()).ToLocalTime()
            : DateTimeOffset.Now;

        return new OilPriceEntry(meta.Code, meta.DisplayName, meta.Unit, price, prev, updatedAt);
    }

    public void Dispose() => _http.Dispose();
}
