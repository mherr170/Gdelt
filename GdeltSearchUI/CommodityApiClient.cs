using System.Text.Json;

namespace GdeltSearchUI;

/// <summary>
/// Fetches energy spot prices from the EIA Open Data API v2.
/// Brent, WTI, and Henry Hub are daily; Heating Oil and RBOB are weekly-only series.
/// Same free API key as the gas-price feature (eia.gov/opendata).
/// </summary>
internal sealed class CommodityApiClient : IDisposable
{
    private const string PetBase = "https://api.eia.gov/v2/petroleum/pri/spt/data/";
    private const string NgBase  = "https://api.eia.gov/v2/natural-gas/pri/fut/data/";

    // Slug, DisplayName, Unit, Series, Frequency ("daily"/"weekly")
    internal static readonly (string Slug, string DisplayName, string Unit, string Series, string Frequency)[] Catalog =
    [
        ("brent_crude_oil", "Brent Crude",   "$/bbl",      "RBRTE",                   "daily"),
        ("crude_oil",       "WTI Crude",     "$/bbl",      "RWTC",                    "daily"),
        ("natural_gas",     "Natural Gas",   "$/MMBtu",    "RNGWHHD",                 "daily"),
        ("heating_oil",     "Heating Oil",   "$/gal (wk)", "EER_EPD2F_PF4_Y35NY_DPG", "weekly"),
        ("gasoline_rbob",   "RBOB Gasoline", "$/gal (wk)", "EER_EPMRR_PF4_Y05LA_DPG", "weekly"),
    ];

    private static readonly Dictionary<string, string> _seriesToSlug =
        Catalog.ToDictionary(c => c.Series, c => c.Slug);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly string _apiKey;

    public CommodityApiClient(string apiKey) => _apiKey = apiKey;

    public async Task<CommodityData> GetAllAsync(int days = 30, CancellationToken ct = default)
    {
        // Three parallel fetches: daily crude+NG, weekly petroleum products
        var dailyPetTask  = FetchAsync(PetBase, ["RBRTE", "RWTC"],                              "daily",  days * 2, ct);
        var ngTask        = FetchAsync(NgBase,  ["RNGWHHD"],                                    "daily",  days,     ct);
        var weeklyPetTask = FetchAsync(PetBase, ["EER_EPD2F_PF4_Y35NY_DPG",
                                                  "EER_EPMRR_PF4_Y05LA_DPG"],                  "weekly", days,     ct);

        List<EiaDataPoint>[] results;
        try { results = await Task.WhenAll(dailyPetTask, ngTask, weeklyPetTask); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new CommodityData { ErrorMessage = $"EIA request failed: {ex.Message}" };
        }

        var allRows = results.SelectMany(r => r).ToList();
        if (allRows.Count == 0)
            return new CommodityData { ErrorMessage = "EIA returned no spot price data." };

        // Build history points grouped by date, oldest-first.
        // Weekly rows land on the same date-key as their published date.
        var history = allRows
            .GroupBy(r => r.Period)
            .OrderBy(g => g.Key)
            .Select(g => new CommodityHistoryPoint
            {
                Timestamp = DateTimeOffset.TryParse(g.Key, out var d) ? d : DateTimeOffset.UtcNow,
                Prices = g
                    .Where(r => _seriesToSlug.ContainsKey(r.Series) && double.TryParse(r.Value, out _))
                    .ToDictionary(
                        r => _seriesToSlug[r.Series],
                        r => double.Parse(r.Value!)),
            })
            .Where(h => h.Prices.Count > 0)
            .ToList();

        if (history.Count == 0)
            return new CommodityData { ErrorMessage = "No parseable spot price records in EIA response." };

        // Most-recent price per slug (may come from different dates for daily vs weekly series)
        var latestBySlug = new Dictionary<string, (double Price, DateTimeOffset Date)>();
        foreach (var point in history)
            foreach (var (slug, price) in point.Prices)
                latestBySlug[slug] = (price, point.Timestamp);

        // Previous price: last history point that has this slug and predates the latest
        var prevBySlug = new Dictionary<string, double>();
        foreach (var (slug, (_, latestDate)) in latestBySlug)
        {
            var prev = history
                .Where(h => h.Prices.ContainsKey(slug) && h.Timestamp < latestDate)
                .LastOrDefault();
            if (prev is not null) prevBySlug[slug] = prev.Prices[slug];
        }

        var prices = Catalog
            .Where(c => latestBySlug.ContainsKey(c.Slug))
            .Select(c =>
            {
                var (price, date) = latestBySlug[c.Slug];
                return new CommodityPrice
                {
                    Slug        = c.Slug,
                    DisplayName = c.DisplayName,
                    Unit        = c.Unit,
                    Price       = price,
                    Previous    = prevBySlug.TryGetValue(c.Slug, out var p) ? p : null,
                    UpdatedAt   = date,
                };
            })
            .ToList();

        return new CommodityData { Prices = prices, History = history };
    }

    private async Task<List<EiaDataPoint>> FetchAsync(
        string endpoint, string[] series, string frequency, int length, CancellationToken ct)
    {
        var seriesFacets = string.Concat(series.Select(s => $"&facets[series][]={Uri.EscapeDataString(s)}"));
        var url = $"{endpoint}?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&frequency={frequency}&data[]=value" +
                  seriesFacets +
                  $"&sort[0][column]=period&sort[0][direction]=desc&length={length}";
        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];
            var json   = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<EiaResponse>(json);
            return parsed?.Response?.Data ?? [];
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    public void Dispose() => _http.Dispose();
}
