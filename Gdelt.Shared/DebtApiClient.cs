using System.Globalization;
using System.Net;
using System.Text.Json;

namespace GdeltSearchUI;

/// <summary>
/// Fetches the latest "Debt to the Penny" figures from the US Treasury
/// Fiscal Data API (no key required, free public endpoint).
/// </summary>
internal sealed class DebtApiClient : IDisposable
{
    private const string Endpoint =
        "https://api.fiscaldata.treasury.gov/services/api/fiscal_service/" +
        "v2/accounting/od/debt_to_penny" +
        "?fields=record_date,debt_held_public_amt,intragov_hold_amt,tot_pub_debt_out_amt" +
        "&sort=-record_date" +
        "&page[size]=2";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<NationalDebt> GetLatestAsync(CancellationToken ct = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(Endpoint, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new NationalDebt { ErrorMessage = $"Request failed: {ex.Message}" };
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds;
            var hint = retryAfter.HasValue ? $" Retry after {retryAfter:F0}s." : "";
            return new NationalDebt
            {
                IsRateLimited = true,
                ErrorMessage  = $"Treasury API rate-limited (HTTP 429).{hint}",
            };
        }

        if (!response.IsSuccessStatusCode)
            return new NationalDebt
            {
                ErrorMessage = $"Treasury API error {(int)response.StatusCode}: {response.ReasonPhrase}",
            };

        var json = await response.Content.ReadAsStringAsync(ct);

        DebtApiResponse? parsed;
        try { parsed = JsonSerializer.Deserialize<DebtApiResponse>(json); }
        catch (JsonException ex)
        {
            return new NationalDebt { ErrorMessage = $"JSON parse error: {ex.Message}" };
        }

        var rows = parsed?.Data;
        if (rows is null || rows.Count == 0)
            return new NationalDebt { ErrorMessage = "Treasury API returned no data." };

        var current  = ToSnapshot(rows[0]);
        var previous = rows.Count >= 2 ? ToSnapshot(rows[1]) : null;

        if (current is null)
            return new NationalDebt { ErrorMessage = "Latest row could not be parsed." };

        return new NationalDebt { Current = current, Previous = previous };
    }

    /// <summary>
    /// Day-over-day percentage change in total public debt outstanding.
    /// Returns null if either snapshot is missing or the previous total is zero.
    /// </summary>
    public static double? PercentChange(NationalDebt debt)
    {
        if (debt.Current is null || debt.Previous is null) return null;
        if (debt.Previous.TotalPublicDebt == 0m) return null;

        var diff = debt.Current.TotalPublicDebt - debt.Previous.TotalPublicDebt;
        return (double)(diff / debt.Previous.TotalPublicDebt) * 100.0;
    }

    private static DebtSnapshot? ToSnapshot(DebtApiRow row)
    {
        if (!DateOnly.TryParse(row.RecordDate, out var date)) return null;
        return new DebtSnapshot
        {
            RecordDate       = date,
            DebtHeldByPublic = ParseDecimal(row.DebtHeldPublicAmt),
            IntragovHoldings = ParseDecimal(row.IntragovHoldAmt),
            TotalPublicDebt  = ParseDecimal(row.TotalPublicDebtOutstandingAmt),
        };
    }

    private static decimal ParseDecimal(string? raw) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    /// <summary>
    /// Fetches the most recent N daily snapshots, oldest-first. Used for sparklines.
    /// </summary>
    public async Task<List<DebtSnapshot>> GetRecentAsync(int days, CancellationToken ct = default)
    {
        var url =
            "https://api.fiscaldata.treasury.gov/services/api/fiscal_service/" +
            "v2/accounting/od/debt_to_penny" +
            "?fields=record_date,debt_held_public_amt,intragov_hold_amt,tot_pub_debt_out_amt" +
            $"&sort=-record_date&page[size]={days}";

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<DebtApiResponse>(json);
            if (parsed?.Data is null) return [];

            return parsed.Data
                .Select(ToSnapshot)
                .Where(s => s is not null)
                .Select(s => s!)
                .OrderBy(s => s.RecordDate)
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    public void Dispose() => _http.Dispose();
}
