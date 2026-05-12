using System.Text.Json;

namespace GdeltSearchUI;

internal sealed class StockApiClient : IDisposable
{
    private const string Base = "https://query1.finance.yahoo.com/v8/finance/chart";

    private readonly HttpClient _http;

    public StockApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
    }

    public async Task<IReadOnlyList<StockEntry>> GetLatestAsync(CancellationToken ct = default)
    {
        var tasks   = StockIndex.Catalog.Select(c => FetchOneAsync(c.Symbol, c.DisplayName, ct)).ToArray();
        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<StockEntry>().ToList();
    }

    private async Task<StockEntry?> FetchOneAsync(string symbol, string displayName, CancellationToken ct)
    {
        try
        {
            var url  = $"{Base}/{Uri.EscapeDataString(symbol)}?interval=5m&range=1d";
            var json = await _http.GetStringAsync(url, ct);
            return Parse(json, symbol, displayName);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            AppLogger.Log($"StockApiClient [{symbol}]: {ex.Message}");
            return null;
        }
    }

    private static StockEntry? Parse(string json, string symbol, string displayName)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("chart", out var chart)) return null;
        if (!chart.TryGetProperty("result", out var results)) return null;
        if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;

        var result = results[0];
        var meta   = result.GetProperty("meta");

        var price    = meta.GetProperty("regularMarketPrice").GetDouble();
        var prevClose= meta.TryGetProperty("previousClose", out var pc) ? pc.GetDouble() : 0.0;
        var mktTime  = meta.TryGetProperty("regularMarketTime", out var ts)
            ? DateTimeOffset.FromUnixTimeSeconds(ts.GetInt64())
            : DateTimeOffset.Now;

        var changePct = prevClose != 0 ? (price - prevClose) / prevClose * 100.0 : 0.0;

        // Parse intraday timestamps + closes
        var intraday = new List<(DateTime, double)>();
        if (result.TryGetProperty("timestamp", out var stamps) &&
            result.TryGetProperty("indicators", out var indicators) &&
            indicators.TryGetProperty("quote", out var quotes) &&
            quotes.ValueKind == JsonValueKind.Array && quotes.GetArrayLength() > 0 &&
            quotes[0].TryGetProperty("close", out var closes))
        {
            var tsArr = stamps.EnumerateArray().ToArray();
            var clArr = closes.EnumerateArray().ToArray();
            for (int i = 0; i < Math.Min(tsArr.Length, clArr.Length); i++)
            {
                if (clArr[i].ValueKind == JsonValueKind.Null) continue;
                var t = DateTimeOffset.FromUnixTimeSeconds(tsArr[i].GetInt64()).LocalDateTime;
                var c = clArr[i].GetDouble();
                if (c > 0) intraday.Add((t, c));
            }
        }

        return new StockEntry(symbol, displayName, price, prevClose, changePct, mktTime, intraday);
    }

    // Returns the trading date in ET, or null if market didn't trade today.
    public static string? TradingDateIfClosed(IReadOnlyList<StockEntry> entries)
    {
        if (entries.Count == 0) return null;

        var eastern  = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var etNow    = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, eastern);

        // Not a weekday
        if (etNow.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return null;

        // Market hasn't closed yet (closes 4 PM ET)
        if (etNow.TimeOfDay < TimeSpan.FromHours(16)) return null;

        // Check the data is actually from today (detects holidays)
        var spx = entries.FirstOrDefault(e => e.Symbol == "^GSPC");
        if (spx is not null)
        {
            var tradeDay = TimeZoneInfo.ConvertTime(spx.UpdatedAt, eastern).Date;
            if (tradeDay != etNow.Date) return null; // holiday — no trading today
        }

        return etNow.Date.ToString("yyyy-MM-dd");
    }

    public void Dispose() => _http.Dispose();
}
