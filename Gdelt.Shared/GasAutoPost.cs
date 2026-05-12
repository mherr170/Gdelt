namespace GdeltSearchUI;

internal enum GasAutoPostOutcome { AlreadyPosted, Posted, MissingApiKey, MissingCredentials, Failed }

internal sealed record GasAutoPostResult(
    GasAutoPostOutcome Outcome,
    string?            Period       = null,
    string?            ErrorMessage = null);

internal static class GasAutoPost
{
    private const string W = "gas";

    public static async Task<GasAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        var apiKey = CredentialManager.LoadEiaApiKey();
        if (apiKey is null)
        {
            PostLogger.Warn(W, "No EIA API key configured — open the UI to add it");
            return new(GasAutoPostOutcome.MissingApiKey);
        }

        PostLogger.Info(W, "Fetching latest EIA gas price data…");
        NationalGasPrices data;
        using (var client = new GasPriceApiClient(apiKey))
        {
            try   { data = await client.GetNationalAveragesAsync(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                PostLogger.Error(W, $"Fetch failed: {ex.Message}");
                return new(GasAutoPostOutcome.Failed, ErrorMessage: ex.Message);
            }
        }

        if (!data.IsSuccess)
        {
            PostLogger.Error(W, $"API error: {data.ErrorMessage}");
            return new(GasAutoPostOutcome.Failed, ErrorMessage: data.ErrorMessage);
        }

        var period = data.Period;
        PostLogger.Info(W, $"Latest period: {period}");

        if (GasPricePostTracker.HasBeenPosted(period))
        {
            PostLogger.Info(W, $"Period {period} already posted — skipping");
            return new(GasAutoPostOutcome.AlreadyPosted, period);
        }

        var creds = CredentialManager.LoadGasPriceBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, $"No Bluesky credentials configured (period {period})");
            return new(GasAutoPostOutcome.MissingCredentials, period);
        }

        PostLogger.Info(W, $"Posting for period {period} as @{creds.Value.Handle}…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateGasPricePostAsync(data);
        var text = BuildPostText(data, headline, tags);

        (bool Ok, string? Error) result;
        using var poster = new BlueskyPoster();

        const int MonthsToShow = 3;
        var monthly = GasPriceChart.ComputeMonthlyAverages(data.History);
        if (monthly.Count > MonthsToShow) monthly = monthly.TakeLast(MonthsToShow).ToList();
        if (monthly.Count >= 2)
        {
            var png = GasPriceChart.RenderMonthlyAveragesPng(data.History, MonthsToShow);
            var alt = BuildAltText(monthly);
            result = await poster.PostTextWithImageAsync(
                creds.Value.Handle, creds.Value.Password, text, png, alt, ct);
        }
        else
        {
            result = await poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, ct);
        }

        if (result.Ok)
        {
            GasPricePostTracker.MarkPosted(period);
            PostLogger.Success(W,
                $"Posted for period {period} | Headline: \"{headline}\" | " +
                $"Regular: {Fmt(data.Regular)} Mid: {Fmt(data.MidGrade)} " +
                $"Prem: {Fmt(data.Premium)} Diesel: {Fmt(data.Diesel)}");
            return new(GasAutoPostOutcome.Posted, period);
        }

        PostLogger.Error(W, $"Post failed for period {period}: {result.Error}");
        return new(GasAutoPostOutcome.Failed, period, result.Error);
    }

    internal static string BuildPostText(NationalGasPrices p, string headline, string[] tags)
    {
        var hashtagLine  = BlueskyPostHelper.HashtagLine(tags);
        var prev         = p.Previous;
        var ya           = p.YearAgo;
        var period       = FmtPeriod(p.Period);
        var prevPeriod   = prev is { Period.Length: > 0 } ? FmtPeriod(prev.Period) : null;

        // YoY is the high-value context — show it immediately under the header
        var yoyLine = (ya is not null && p.Regular.HasValue && ya.Regular.HasValue)
            ? $"vs 1yr ago: Regular {(p.Regular.Value - ya.Regular.Value >= 0 ? "+" : "-")}${Math.Abs(p.Regular.Value - ya.Regular.Value):F2}\n"
            : "";

        var footer = prevPeriod is not null
            ? $"vs week of {prevPeriod} · EIA{hashtagLine}"
            : $"EIA{hashtagLine}";

        return $"{headline}\n\n" +
               $"⛽ Week of {period} ⛽\n" +
               yoyLine +
               $"\n" +
               $"{Bold("Regular")}: {Bold(Fmt(p.Regular))}/gal {DeltaText(p.Regular, prev?.Regular)}\n" +
               $"{Bold("Premium")}: {Bold(Fmt(p.Premium))}/gal {DeltaText(p.Premium, prev?.Premium)}\n" +
               $"{Bold("Diesel")}:  {Bold(Fmt(p.Diesel))}/gal {DeltaText(p.Diesel,   prev?.Diesel)}\n\n" +
               footer;
    }

    private static string FmtPeriod(string period) =>
        DateTime.TryParse(period, out var dt) ? dt.ToString("MMM d") : period;

    internal static string BuildAltText(IReadOnlyList<MonthlyAverage> monthly)
    {
        var first = monthly[0];
        var last  = monthly[^1];

        string Trend(double? a, double? b)
        {
            if (!a.HasValue || !b.HasValue) return "no data";
            var d = b.Value - a.Value;
            var dir = d > 0.005 ? "up" : d < -0.005 ? "down" : "flat";
            return $"{dir} {(d >= 0 ? "+" : "-")}${Math.Abs(d):F2}";
        }

        return
            $"Multi-line chart titled \"US Gas Prices — Monthly Averages (Last {monthly.Count} Months)\". " +
            $"X-axis shows months from {first.Month:yyyy-MM} to {last.Month:yyyy-MM}. " +
            $"Y-axis shows price per gallon in US dollars. " +
            $"Three lines plot Regular, Premium, and Diesel. " +
            $"Over the period, Regular went {Trend(first.Regular, last.Regular)}, " +
            $"Premium went {Trend(first.Premium, last.Premium)}, " +
            $"Diesel went {Trend(first.Diesel, last.Diesel)}. " +
            $"Source: US Energy Information Administration.";
    }

    private static string Fmt(double? v) => v.HasValue ? $"${v.Value:F3}" : "N/A";
    private static string DeltaText(double? curr, double? prev) => BlueskyPostHelper.DeltaTextAbsolute(curr, prev);
    private static string Bold(string s) => BlueskyPostHelper.Bold(s);
}
