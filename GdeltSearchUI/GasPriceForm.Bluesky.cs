namespace GdeltSearchUI;

internal partial class GasPriceForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_lastResult is null) return;

        var creds = CredentialManager.LoadGasPriceBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadGasPriceBluesky,
                CredentialManager.SaveGasPriceBluesky,
                "Bluesky Account — Gas Prices");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadGasPriceBluesky();
            if (creds is null) return;
        }

        _postButton.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateGasPricePostAsync(_lastResult);

        var text = BuildPostText(_lastResult, headline, tags);
        (bool ok, string? error) result;

        const int MonthsToShow = 3;
        var monthly = GasPriceChart.ComputeMonthlyAverages(_lastResult.History);
        if (monthly.Count > MonthsToShow) monthly = monthly.TakeLast(MonthsToShow).ToList();
        if (monthly.Count >= 2)
        {
            SetStatus("Rendering monthly-averages chart and posting to Bluesky…");
            var png = GasPriceChart.RenderMonthlyAveragesPng(_lastResult.History, MonthsToShow);
            var alt = BuildAltText(monthly);
            result = await _poster.PostTextWithImageAsync(
                creds.Value.Handle, creds.Value.Password, text, png, alt, CancellationToken.None);
        }
        else
        {
            SetStatus("Posting to Bluesky (text only — insufficient history)…");
            result = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
        }
        var (ok, error) = result;

        if (ok)
        {
            GasPricePostTracker.MarkPosted(_lastResult.Period);
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }

    private static string BuildPostText(NationalGasPrices p, string headline, string[] tags)
    {
        var hashtagLine = BlueskyPostHelper.HashtagLine(tags);
        var prev = p.Previous;
        var ya   = p.YearAgo;
        var yoyLine = (ya is not null && p.Regular.HasValue && ya.Regular.HasValue)
            ? $"vs 1yr ago: {(p.Regular.Value - ya.Regular.Value >= 0 ? "+" : "-")}${Math.Abs(p.Regular.Value - ya.Regular.Value):F2} (Regular)\n"
            : "";
        var footer = prev is { Period.Length: > 0 }
            ? $"{BlueskyPostHelper.Divider}\nvs week of {prev.Period}\n{yoyLine}"
            : yoyLine;
        return $"{headline}\n\n" +
               $"⛽ Week of {p.Period} ⛽\n\n" +
               $"{Bold("Regular")}:   {Bold(Fmt(p.Regular))}/gal {DeltaText(p.Regular,  prev?.Regular)}\n" +
               $"{Bold("Mid-Grade")}: {Bold(Fmt(p.MidGrade))}/gal {DeltaText(p.MidGrade, prev?.MidGrade)}\n" +
               $"{Bold("Premium")}:   {Bold(Fmt(p.Premium))}/gal {DeltaText(p.Premium,  prev?.Premium)}\n" +
               $"{Bold("Diesel")}:    {Bold(Fmt(p.Diesel))}/gal {DeltaText(p.Diesel,    prev?.Diesel)}\n\n" +
               footer +
               $"Source: EIA{hashtagLine}";
    }

    private static string BuildAltText(IReadOnlyList<MonthlyAverage> monthly)
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
            $"Four lines plot Regular, Mid-Grade, Premium, and Diesel. " +
            $"Over the period, Regular went {Trend(first.Regular, last.Regular)}, " +
            $"Mid-Grade went {Trend(first.MidGrade, last.MidGrade)}, " +
            $"Premium went {Trend(first.Premium, last.Premium)}, " +
            $"Diesel went {Trend(first.Diesel, last.Diesel)}. " +
            $"Source: US Energy Information Administration.";
    }

    private static string DeltaText(double? curr, double? prev) => BlueskyPostHelper.DeltaTextAbsolute(curr, prev);
    private static string Bold(string s)                        => BlueskyPostHelper.Bold(s);
}
