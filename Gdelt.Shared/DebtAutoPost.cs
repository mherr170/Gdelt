namespace GdeltSearchUI;

internal enum DebtAutoPostOutcome { AlreadyPosted, Posted, MissingCredentials, Failed }

internal sealed record DebtAutoPostResult(
    DebtAutoPostOutcome Outcome,
    string?             RecordDate   = null,
    string?             ErrorMessage = null);

internal static class DebtAutoPost
{
    private const string W = "debt";

    public static async Task<DebtAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, "Fetching latest Treasury data…");
        NationalDebt data;
        using (var client = new DebtApiClient())
        {
            try   { data = await client.GetLatestAsync(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                PostLogger.Error(W, $"Fetch failed: {ex.Message}");
                return new(DebtAutoPostOutcome.Failed, ErrorMessage: ex.Message);
            }
        }

        if (!data.IsSuccess)
        {
            PostLogger.Error(W, $"API error: {data.ErrorMessage}");
            return new(DebtAutoPostOutcome.Failed, ErrorMessage: data.ErrorMessage);
        }

        var recordDate = data.Current!.RecordDate.ToString("yyyy-MM-dd");
        PostLogger.Info(W, $"Latest record date: {recordDate}");

        if (DebtPostTracker.HasBeenPosted(recordDate))
        {
            PostLogger.Info(W, $"Record date {recordDate} already posted — skipping");
            return new(DebtAutoPostOutcome.AlreadyPosted, recordDate);
        }

        var creds = CredentialManager.LoadDebtBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, $"No Bluesky credentials configured (record date {recordDate})");
            return new(DebtAutoPostOutcome.MissingCredentials, recordDate);
        }

        PostLogger.Info(W, $"Posting for {recordDate} as @{creds.Value.Handle}…");

        List<DebtSnapshot> history;
        using (var client = new DebtApiClient())
            history = await client.GetHistorySinceAsync(DebtApiClient.HistoryStart, ct);

        var (headline, tags) = await LmStudioPostGenerator.GenerateDebtPostAsync(data);
        var text = BuildPostText(data, headline, tags);

        (bool Ok, string? Error) result;
        using var poster = new BlueskyPoster();
        if (history.Count >= 2)
        {
            var png = DebtSparkline.RenderPng(history);
            var alt = BuildAltText(history);
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
            DebtPostTracker.MarkPosted(recordDate);
            var c = data.Current!;
            PostLogger.Success(W,
                $"Posted for {recordDate} | Headline: \"{headline}\" | " +
                $"Total: {FmtTrillions(c.TotalPublicDebt)} " +
                $"Public: {FmtTrillions(c.DebtHeldByPublic)} " +
                $"Intragov: {FmtTrillions(c.IntragovHoldings)}");
            return new(DebtAutoPostOutcome.Posted, recordDate);
        }
        PostLogger.Error(W, $"Post failed for {recordDate}: {result.Error}");
        return new(DebtAutoPostOutcome.Failed, recordDate, result.Error);
    }

    internal static string BuildPostText(NationalDebt d, string headline, string[] tags)
    {
        var c = d.Current!;
        var p = d.Previous;
        var hashtagLine = BlueskyPostHelper.HashtagLine(tags);
        var pct = DebtApiClient.PercentChange(d);

        var deltaLine = "";
        if (p is not null)
        {
            var diff   = c.TotalPublicDebt - p.TotalPublicDebt;
            var icon   = diff > 0 ? "📈" : diff < 0 ? "📉" : "➖";
            var pctStr = pct.HasValue ? $" ({(pct >= 0 ? "+" : "")}{pct:F4}%)" : "";
            deltaLine  = $"{BlueskyPostHelper.Divider}\n{icon} Day-over-day: {FmtBillionsDelta(diff)}{pctStr}\nvs {p.RecordDate:yyyy-MM-dd}\n";
        }

        return $"{headline}\n\n" +
               $"🇺🇸 As of {c.RecordDate:yyyy-MM-dd} 🇺🇸\n\n" +
               $"{Bold("Total Debt")}:    {Bold(FmtTrillions(c.TotalPublicDebt))}\n" +
               $"{Bold("Held by Public")}: {Bold(FmtTrillions(c.DebtHeldByPublic))}\n" +
               $"{Bold("Intragov")}:       {Bold(FmtTrillions(c.IntragovHoldings))}\n\n" +
               deltaLine +
               $"Source: US Treasury{hashtagLine}";
    }

    internal static string BuildAltText(IReadOnlyList<DebtSnapshot> history)
    {
        var first = history[0];
        var last  = history[^1];
        var startT = first.TotalPublicDebt / 1_000_000_000_000m;
        var endT   = last.TotalPublicDebt  / 1_000_000_000_000m;
        var diffB  = (last.TotalPublicDebt - first.TotalPublicDebt) / 1_000_000_000m;
        var pct    = first.TotalPublicDebt > 0
            ? (double)((last.TotalPublicDebt - first.TotalPublicDebt) / first.TotalPublicDebt) * 100.0
            : 0.0;

        var direction = diffB switch { > 0 => "rising", < 0 => "falling", _ => "flat" };
        var sign      = diffB >= 0 ? "+" : "-";
        var min       = history.Min(s => s.TotalPublicDebt) / 1_000_000_000_000m;
        var max       = history.Max(s => s.TotalPublicDebt) / 1_000_000_000_000m;

        return
            $"Line chart titled \"US National Debt — {first.RecordDate:MMM d, yyyy} to {last.RecordDate:MMM d, yyyy}\". " +
            $"X-axis shows dates from {first.RecordDate:yyyy-MM-dd} to {last.RecordDate:yyyy-MM-dd}. " +
            $"Y-axis shows total public debt in trillions of dollars, ranging from ${min:F3}T to ${max:F3}T. " +
            $"The line is {direction}, starting at ${startT:F3}T and ending at ${endT:F3}T, " +
            $"a change of {sign}${Math.Abs(diffB):F2} billion ({(pct >= 0 ? "+" : "")}{pct:F4}%) over the period. " +
            $"Source: US Treasury Fiscal Data API.";
    }

    private static string FmtTrillions(decimal v) => $"${v / 1_000_000_000_000m:F3}T";

    private static string FmtBillionsDelta(decimal v)
    {
        var b    = v / 1_000_000_000m;
        var sign = b >= 0 ? "+" : "-";
        return $"{sign}${Math.Abs(b):F2}B";
    }

    private static string Bold(string s) => BlueskyPostHelper.Bold(s);
}
