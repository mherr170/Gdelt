namespace GdeltSearchUI;

internal enum YahooAutoPostOutcome { Posted, MissingCredentials, Failed }

internal sealed record YahooAutoPostResult(
    YahooAutoPostOutcome Outcome,
    string?              ErrorMessage = null,
    DateTime?            LastPostedAt = null);

internal static class YahooAutoPost
{
    private const string W = "yahoo";

    public static async Task<YahooAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, "Fetching latest Yahoo Finance energy futures…");
        IReadOnlyList<OilPriceEntry> prices;
        try
        {
            using var client = new YahooFinanceApiClient();
            prices = await client.GetLatestAsync();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"Fetch failed: {ex.Message}");
            return new(YahooAutoPostOutcome.Failed, ex.Message);
        }

        if (prices.Count == 0)
        {
            PostLogger.Error(W, "No price data returned from Yahoo Finance");
            return new(YahooAutoPostOutcome.Failed, "No price data returned from Yahoo Finance.");
        }

        var creds = CredentialManager.LoadYahooBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(YahooAutoPostOutcome.MissingCredentials);
        }

        PostLogger.Info(W, $"Posting as @{creds.Value.Handle} ({prices.Count} tickers)…");

        var savedHistory   = YahooPriceCache.Load();
        var lastPostPrices = savedHistory.LastOrDefault()?.Prices;

        // Save snapshot before posting — data preserved even if caption/post fails
        var snapshot = YahooPriceCache.Append(prices);
        var previewHistory = savedHistory.Append(snapshot).ToList();

        string headline; string[] tags;
        try   { (headline, tags) = await LmStudioPostGenerator.GenerateYahooFuturesPostAsync(prices, lastPostPrices); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"Caption generation failed: {ex.Message}");
            return new(YahooAutoPostOutcome.Failed, $"Caption failed: {ex.Message}");
        }

        var text = BuildPostText(prices, headline, tags, lastPostPrices);

        byte[] png;
        try   { png = await Task.Run(() => YahooChartGenerator.RenderPng(previewHistory), ct); }
        catch (OperationCanceledException) { throw; }
        catch                              { png = []; }

        (bool Ok, string? Error) result;
        using var poster = new BlueskyPoster();
        try
        {
            result = png.Length > 0
                ? await poster.PostTextWithImageAsync(
                    creds.Value.Handle, creds.Value.Password, text,
                    png, BuildAltText(prices, previewHistory), ct)
                : await poster.PostTextAsync(
                    creds.Value.Handle, creds.Value.Password, text, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)               { return new(YahooAutoPostOutcome.Failed, ex.Message); }

        if (result.Ok)
        {
            YahooPostTracker.MarkPosted(DateTime.Today.ToString("yyyy-MM-dd"));
            var summary = string.Join(" | ", prices.Select(e =>
                $"{e.DisplayName}: {FmtPrice(e)}"));
            PostLogger.Success(W,
                $"Posted | Headline: \"{headline}\" | {summary}");
            return new(YahooAutoPostOutcome.Posted, LastPostedAt: DateTime.Now);
        }
        PostLogger.Error(W, $"Post failed: {result.Error}");
        return new(YahooAutoPostOutcome.Failed, result.Error);
    }

    internal static string BuildPostText(
        IReadOnlyList<OilPriceEntry> prices, string headline, string[] tags,
        Dictionary<string, double>?  lastPostPrices = null)
    {
        var allTags     = tags.Prepend("Commodities").Prepend("OilMarkets").Prepend("OOTT").Distinct().ToArray();
        var hashtagLine = BlueskyPostHelper.HashtagLine(allTags);

        string Row(string code)
        {
            var e = prices.FirstOrDefault(p => p.Code == code);
            if (e is null) return "";
            double? baseline = lastPostPrices?.TryGetValue(code, out var lp) == true && lp != 0 ? lp : e.Previous;
            return $"{Bold(e.DisplayName)}: {FmtPrice(e)} {Delta(e.Price, baseline)}\n";
        }

        return $"🛢️ {DateTime.Now:MMM d, yyyy h:mm tt} 🛢️\n{headline}\n\n" +
               Row("BRENT_CRUDE") +
               Row("WTI_CRUDE") +
               Row("NATURAL_GAS") +
               Row("RBOB_GASOLINE") +
               Row("HEATING_OIL") +
               $"\n{BlueskyPostHelper.Divider}\n" +
               $"Src: Yahoo Finance{hashtagLine}";
    }

    internal static string BuildAltText(
        IReadOnlyList<OilPriceEntry> prices, IReadOnlyList<CommodityHistoryPoint> history)
    {
        var n    = history.Count;
        var span = n >= 2
            ? $" from {history[0].Timestamp.LocalDateTime:yyyy-MM-dd HH:mm}" +
              $" to {history[^1].Timestamp.LocalDateTime:yyyy-MM-dd HH:mm}"
            : "";

        var parts = prices
            .Where(e => e.Previous.HasValue && e.Previous.Value != 0)
            .Select(e =>
            {
                var pct = (e.Price - e.Previous!.Value) / e.Previous.Value * 100.0;
                return $"{e.DisplayName}: {(pct >= 0 ? "+" : "")}{pct:F2}% vs prior close";
            });

        return $"Line chart of energy futures % change across {n} post{(n != 1 ? "s" : "")}{span}. " +
               "Each series normalised to 0% at first snapshot. " +
               string.Join(", ", parts) + ". Source: Yahoo Finance.";
    }

    private static string FmtPrice(OilPriceEntry e) => e.Code switch
    {
        "NATURAL_GAS"                     => $"${e.Price:F3}",
        "RBOB_GASOLINE" or "HEATING_OIL" => $"${e.Price:F3}",
        _                                 => $"${e.Price:F2}",
    };

    private static string Delta(double curr, double? prev)
    {
        if (!prev.HasValue || prev.Value == 0) return "";
        var pct = (curr - prev.Value) / prev.Value * 100.0;
        if (Math.Abs(pct) < 0.005) return "➖ 0.00%";
        return $"{(pct > 0 ? "📈" : "📉")} {(pct > 0 ? "+" : "")}{pct:F2}%";
    }

    private static string Bold(string s) => BlueskyPostHelper.Bold(s);
}
