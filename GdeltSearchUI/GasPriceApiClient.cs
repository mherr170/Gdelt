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

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
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
                  $"&length=4";

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

        double? Find(string code)
        {
            var raw = data.FirstOrDefault(d => d.Product == code)?.Value;
            return double.TryParse(raw, out var v) ? v : null;
        }

        var period = data.Max(d => d.Period) ?? "";

        return new NationalGasPrices
        {
            Regular  = Find(Regular),
            MidGrade = Find(MidGrade),
            Premium  = Find(Premium),
            Diesel   = Find(Diesel),
            Period   = period,
        };
    }

    public void Dispose() => _http.Dispose();
}
