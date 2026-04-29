namespace GdeltSearchUI;

internal partial class CommodityForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_lastResult is null) return;

        var creds = CredentialManager.LoadCommodityBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadCommodityBluesky,
                CredentialManager.SaveCommodityBluesky,
                "Bluesky Account — Commodities");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadCommodityBluesky();
            if (creds is null) return;
        }

        _postButton.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateCommodityPostAsync(_lastResult);
        var text = BuildPostText(_lastResult, headline, tags);
        (bool ok, string? error) result;

        var history = _lastResult.History;
        if (history.Count >= 2)
        {
            var png = CommoditySparkline.RenderPng(history);
            if (png.Length > 0)
            {
                SetStatus("Rendering sparkline and posting to Bluesky…");
                var alt = BuildAltText(_lastResult, history);
                result = await _poster.PostTextWithImageAsync(
                    creds.Value.Handle, creds.Value.Password, text, png, alt, CancellationToken.None);
            }
            else
            {
                SetStatus("Posting to Bluesky (text only — chart empty)…");
                result = await _poster.PostTextAsync(
                    creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
            }
        }
        else
        {
            SetStatus("Posting to Bluesky (text only — insufficient history for chart)…");
            result = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
        }

        var (ok, error) = result;
        if (ok)
        {
            CommodityPostTracker.MarkPosted(DateTime.Today.ToString("yyyy-MM-dd"));
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }

    private static string BuildPostText(CommodityData data, string headline, string[] tags)
    {
        var hashtagLine = BlueskyPostHelper.HashtagLine(tags);

        string Row(string slug)
        {
            var p = data.Prices.FirstOrDefault(x => x.Slug == slug);
            if (p is null) return "";
            return $"{Bold(p.DisplayName)}: {Bold(FmtPrice(p))} {p.Unit} {DeltaText(p.Price, p.Previous)}\n";
        }

        return $"{headline}\n\n" +
               Row("brent_crude_oil") +
               Row("crude_oil") +
               Row("natural_gas") +
               Row("heating_oil") +
               Row("gasoline_rbob") +
               $"\n{BlueskyPostHelper.Divider}\n" +
               $"Source: EIA{hashtagLine}";
    }

    private static string BuildAltText(CommodityData data, IReadOnlyList<CommodityHistoryPoint> history)
    {
        var first = history[0];
        var last  = history[^1];

        string PctChange(string slug)
        {
            if (!first.Prices.TryGetValue(slug, out var f) || !last.Prices.TryGetValue(slug, out var l) || f == 0)
                return "n/a";
            var pct = (l - f) / f * 100.0;
            return $"{(pct >= 0 ? "+" : "")}{pct:F1}%";
        }

        return
            $"Multi-line chart showing daily energy spot price % change across {history.Count} sessions " +
            $"from {first.Timestamp:yyyy-MM-dd} to {last.Timestamp:yyyy-MM-dd}. " +
            $"Each series is normalised to 0% at the first snapshot. " +
            $"Brent crude: {PctChange("brent_crude_oil")}, " +
            $"WTI crude: {PctChange("crude_oil")}, " +
            $"Natural gas (Henry Hub): {PctChange("natural_gas")}. " +
            $"Source: EIA.";
    }

    private static string DeltaText(double curr, double? prev) => BlueskyPostHelper.DeltaTextPercent(curr, prev);
    private static string Bold(string s)                       => BlueskyPostHelper.Bold(s);

    // ── Yahoo Finance posting ─────────────────────────────────────────────────

    private async Task PostYahooToBlueskyAsync()
    {
        var prices = _lastResult?.OilPrices;
        if (prices is null || prices.Count == 0) return;

        var creds = CredentialManager.LoadYahooBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadYahooBluesky,
                CredentialManager.SaveYahooBluesky,
                "Bluesky Account — Yahoo Finance Futures");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadYahooBluesky();
            if (creds is null) return;
        }

        _yahooPostButton.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateYahooFuturesPostAsync(prices);
        var text = BuildYahooPostText(prices, headline, tags);

        SetStatus("Posting Yahoo futures to Bluesky…");
        var (ok, error) = await _poster.PostTextAsync(
            creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);

        if (ok)
        {
            YahooPostTracker.MarkPosted(DateTime.Today.ToString("yyyy-MM-dd"));
            SetStatus($"Posted Yahoo futures to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Yahoo post failed — see details.");
        }

        UpdateYahooPostButton();
    }

    private static string BuildYahooPostText(
        IReadOnlyList<OilPriceEntry> prices, string headline, string[] tags)
    {
        var hashtagLine = BlueskyPostHelper.HashtagLine(tags);

        string Row(string code)
        {
            var e = prices.FirstOrDefault(p => p.Code == code);
            if (e is null) return "";
            return $"{Bold(e.DisplayName)}: {Bold(FmtOilPrice(e))} {e.Unit} {DeltaText(e.Price, e.Previous)}\n";
        }

        var freshest = prices.Count > 0 ? prices.Max(p => p.UpdatedAt) : default;
        var asOf     = freshest != default
            ? $"As of {freshest.LocalDateTime:HH:mm} (~15 min delayed)"
            : "~15 min delayed";

        return $"{headline}\n\n" +
               Row("BRENT_CRUDE") +
               Row("WTI_CRUDE") +
               Row("NATURAL_GAS") +
               Row("RBOB_GASOLINE") +
               Row("HEATING_OIL") +
               $"\n{BlueskyPostHelper.Divider}\n" +
               $"Source: Yahoo Finance · {asOf}{hashtagLine}";
    }
}
