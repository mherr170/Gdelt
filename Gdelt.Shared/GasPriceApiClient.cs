using System.Text.Json;

namespace GdeltSearchUI;

/// <summary>
/// Fetches US national average retail gas prices from the EIA Open Data API v2.
/// Free API key: https://www.eia.gov/opendata/register.php
/// </summary>
internal sealed class GasPriceApiClient : IDisposable
{
    // Product codes used by EIA for retail pump prices
    private const string Regular  = "EPM0";
    private const string MidGrade = "EPMM";
    private const string Premium  = "EPMP";
    private const string Diesel   = "EPD2D";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly string _apiKey;

    public GasPriceApiClient(string apiKey) => _apiKey = apiKey;

    public async Task<NationalGasPrices> GetNationalAveragesAsync(CancellationToken ct = default)
    {
        var url = $"https://api.eia.gov/v2/petroleum/pri/gnd/data/" +
                  $"?api_key={Uri.EscapeDataString(_apiKey)}" +
                  $"&frequency=weekly" +
                  $"&data[]=value" +
                  $"&facets[duoarea][]=NUS" +
                  $"&facets[product][]={Regular}" +
                  $"&facets[product][]={MidGrade}" +
                  $"&facets[product][]={Premium}" +
                  $"&facets[product][]={Diesel}" +
                  $"&sort[0][column]=period&sort[0][direction]=desc" +
                  $"&length=220";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new NationalGasPrices { ErrorMessage = $"Request failed: {ex.Message}" };
        }

        if (!response.IsSuccessStatusCode)
            return new NationalGasPrices
            {
                ErrorMessage = $"EIA API error {(int)response.StatusCode}: {response.ReasonPhrase}"
            };

        var json = await response.Content.ReadAsStringAsync(ct);

        EiaResponse? parsed;
        try { parsed = JsonSerializer.Deserialize<EiaResponse>(json); }
        catch (JsonException ex)
        {
            return new NationalGasPrices { ErrorMessage = $"JSON parse error: {ex.Message}" };
        }

        var data = parsed?.Response?.Data;
        if (data is null || data.Count == 0)
            return new NationalGasPrices { ErrorMessage = "EIA returned no data." };

        // EIA returns rows sorted desc by period. Distinct periods, newest first.
        var periods = data.Select(d => d.Period)
                          .Where(p => !string.IsNullOrEmpty(p))
                          .Distinct()
                          .OrderByDescending(p => p)
                          .ToList();

        if (periods.Count == 0)
            return new NationalGasPrices { ErrorMessage = "EIA returned no usable periods." };

        double? Find(string code, string period)
        {
            var raw = data.FirstOrDefault(d => d.Product == code && d.Period == period)?.Value;
            return double.TryParse(raw, out var v) ? v : null;
        }

        NationalGasPrices Build(string period) => new()
        {
            Regular  = Find(Regular,  period),
            MidGrade = Find(MidGrade, period),
            Premium  = Find(Premium,  period),
            Diesel   = Find(Diesel,   period),
            Period   = period,
        };

        var current  = Build(periods[0]);
        var previous = periods.Count >= 2 ? Build(periods[1]) : null;

        // Year-ago: closest period to ~52 weeks before current.
        NationalGasPrices? yearAgo = null;
        if (DateTime.TryParse(periods[0], out var currentDate))
        {
            var target = currentDate.AddDays(-364);
            var match = periods
                .Select(p => (period: p, parsed: DateTime.TryParse(p, out var d) ? d : (DateTime?)null))
                .Where(x => x.parsed.HasValue)
                .OrderBy(x => Math.Abs((x.parsed!.Value - target).TotalDays))
                .FirstOrDefault();
            if (match.period is not null && Math.Abs((match.parsed!.Value - target).TotalDays) <= 21)
                yearAgo = Build(match.period);
        }

        // Full history (oldest-first) — used for charting.
        var history = periods
            .OrderBy(p => p)
            .Select(Build)
            .ToList();

        return current with { Previous = previous, YearAgo = yearAgo, History = history };
    }

    public void Dispose() => _http.Dispose();
}
