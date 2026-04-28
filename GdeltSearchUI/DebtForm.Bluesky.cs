namespace GdeltSearchUI;

internal partial class DebtForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_lastResult?.Current is null) return;

        var creds = CredentialManager.LoadDebtBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadDebtBluesky,
                CredentialManager.SaveDebtBluesky,
                "Bluesky Account — National Debt");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadDebtBluesky();
            if (creds is null) return;
        }

        _postButton.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateDebtPostAsync(_lastResult);

        SetStatus("Fetching 7-day history for chart…");
        List<DebtSnapshot> history;
        using (var client = new DebtApiClient())
            history = await client.GetRecentAsync(7);

        var text = BuildPostText(_lastResult, headline, tags);
        (bool ok, string? error) result;

        if (history.Count >= 2)
        {
            SetStatus("Rendering chart and posting to Bluesky…");
            var png = DebtSparkline.RenderPng(history);
            var alt = BuildAltText(history);
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
            DebtPostTracker.MarkPosted(_lastResult.Current.RecordDate.ToString("yyyy-MM-dd"));
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }

    private static string BuildPostText(NationalDebt d, string headline, string[] tags)
    {
        var c = d.Current!;
        var p = d.Previous;
        var hashtagLine = BlueskyPostHelper.HashtagLine(tags);
        var pct = DebtApiClient.PercentChange(d);

        var deltaLine = "";
        if (p is not null)
        {
            var diff = c.TotalPublicDebt - p.TotalPublicDebt;
            var icon = diff > 0 ? "📈" : diff < 0 ? "📉" : "➖";
            var pctStr = pct.HasValue ? $" ({(pct >= 0 ? "+" : "")}{pct:F4}%)" : "";
            deltaLine = $"{BlueskyPostHelper.Divider}\n{icon} Day-over-day: {FmtBillionsDelta(diff)}{pctStr}\nvs {p.RecordDate:yyyy-MM-dd}\n";
        }

        return $"{headline}\n\n" +
               $"🇺🇸 As of {c.RecordDate:yyyy-MM-dd} 🇺🇸\n\n" +
               $"{Bold("Total Debt")}:    {Bold(FmtTrillions(c.TotalPublicDebt))}\n" +
               $"{Bold("Held by Public")}: {Bold(FmtTrillions(c.DebtHeldByPublic))}\n" +
               $"{Bold("Intragov")}:       {Bold(FmtTrillions(c.IntragovHoldings))}\n\n" +
               deltaLine +
               $"Source: US Treasury{hashtagLine}";
    }

    private static string BuildAltText(IReadOnlyList<DebtSnapshot> history)
    {
        var first = history[0];
        var last  = history[^1];
        var startT = first.TotalPublicDebt / 1_000_000_000_000m;
        var endT   = last.TotalPublicDebt  / 1_000_000_000_000m;
        var diffB  = (last.TotalPublicDebt - first.TotalPublicDebt) / 1_000_000_000m;
        var pct    = first.TotalPublicDebt > 0
            ? (double)((last.TotalPublicDebt - first.TotalPublicDebt) / first.TotalPublicDebt) * 100.0
            : 0.0;

        var direction = diffB switch
        {
            > 0 => "rising",
            < 0 => "falling",
            _   => "flat",
        };
        var sign = diffB >= 0 ? "+" : "-";

        var min = history.Min(s => s.TotalPublicDebt) / 1_000_000_000_000m;
        var max = history.Max(s => s.TotalPublicDebt) / 1_000_000_000_000m;

        return
            $"Line chart titled \"US National Debt — Last {history.Count} Days\". " +
            $"X-axis shows dates from {first.RecordDate:yyyy-MM-dd} to {last.RecordDate:yyyy-MM-dd}. " +
            $"Y-axis shows total public debt in trillions of dollars, ranging from ${min:F3}T to ${max:F3}T. " +
            $"The line is {direction}, starting at ${startT:F3}T and ending at ${endT:F3}T, " +
            $"a change of {sign}${Math.Abs(diffB):F2} billion ({(pct >= 0 ? "+" : "")}{pct:F4}%) over the period. " +
            $"Source: US Treasury Fiscal Data API.";
    }

    private static string Bold(string s) => BlueskyPostHelper.Bold(s);
}
