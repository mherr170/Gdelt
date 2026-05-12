namespace GdeltSearchUI;

internal enum StockAutoPostOutcome { Posted, AlreadyPosted, MarketNotClosed, MissingCredentials, Failed }

internal sealed record StockAutoPostResult(
    StockAutoPostOutcome Outcome,
    string?              TradingDate  = null,
    string?              ErrorMessage = null);

internal static class StockAutoPost
{
    private const string W = "stocks";

    public static async Task<StockAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, "Fetching US stock index data…");

        IReadOnlyList<StockEntry> entries;
        try
        {
            using var client = new StockApiClient();
            entries = await client.GetLatestAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"Fetch failed: {ex.Message}");
            return new(StockAutoPostOutcome.Failed, ErrorMessage: ex.Message);
        }

        var tradingDate = StockApiClient.TradingDateIfClosed(entries);
        if (tradingDate is null)
        {
            PostLogger.Info(W, "Market not closed yet or no trading today — skipping");
            return new(StockAutoPostOutcome.MarketNotClosed);
        }

        if (StockPostTracker.HasBeenPosted(tradingDate))
        {
            PostLogger.Info(W, $"Already posted for {tradingDate} — skipping");
            return new(StockAutoPostOutcome.AlreadyPosted, tradingDate);
        }

        var creds = CredentialManager.LoadStockBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(StockAutoPostOutcome.MissingCredentials, tradingDate);
        }

        PostLogger.Info(W, $"Posting close for {tradingDate}…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateStockPostAsync(entries);
        var text = BuildPostText(entries, tradingDate, headline, tags);

        byte[] png;
        try   { png = await Task.Run(() => StockChartGenerator.RenderPng(entries), ct); }
        catch { png = []; }

        using var poster = new BlueskyPoster();
        (bool Ok, string? Error) result;

        if (png.Length > 0)
        {
            var alt = BuildAltText(entries, tradingDate);
            result  = await poster.PostTextWithImageAsync(
                creds.Value.Handle, creds.Value.Password, text, png, alt, ct);
        }
        else
        {
            result = await poster.PostTextAsync(creds.Value.Handle, creds.Value.Password, text, ct);
        }

        if (result.Ok)
        {
            StockPostTracker.MarkPosted(tradingDate);
            PostLogger.Success(W, $"Posted close for {tradingDate} | {string.Join(" | ", entries.Select(e => $"{e.DisplayName}: {e.ChangePercent:+0.00;-0.00}%"))}");
            return new(StockAutoPostOutcome.Posted, tradingDate);
        }

        PostLogger.Error(W, $"Post failed: {result.Error}");
        return new(StockAutoPostOutcome.Failed, tradingDate, result.Error);
    }

    internal static string BuildPostText(
        IReadOnlyList<StockEntry> entries, string tradingDate, string headline, string[] tags)
    {
        var allTags     = tags.Prepend("WallStreet").Prepend("StockMarket").Distinct().ToArray();
        var hashtagLine = BlueskyPostHelper.HashtagLine(allTags);

        string Row(string symbol)
        {
            var e = entries.FirstOrDefault(x => x.Symbol == symbol);
            if (e is null) return "";
            var icon = e.ChangePercent > 0 ? "📈" : e.ChangePercent < 0 ? "📉" : "➖";
            var sign = e.ChangePercent >= 0 ? "+" : "";
            return $"{BlueskyPostHelper.Bold(e.DisplayName)}: {FmtPrice(e)} {icon} {sign}{e.ChangePercent:F2}%\n";
        }

        return
            $"📊 Market Close — {tradingDate}\n" +
            $"{headline}\n\n" +
            Row("^GSPC") +
            Row("^DJI") +
            Row("^IXIC") +
            Row("^RUT") +
            $"\n{BlueskyPostHelper.Divider}\n" +
            $"Src: Yahoo Finance{hashtagLine}";
    }

    internal static string BuildAltText(IReadOnlyList<StockEntry> entries, string tradingDate)
    {
        var parts = entries.Select(e =>
            $"{e.DisplayName}: {e.ChangePercent:+0.00;-0.00}% vs prior close");
        return
            $"Intraday line chart of US stock indices on {tradingDate}, " +
            "each series normalised to 0% at prior close. " +
            string.Join(", ", parts) + ". Source: Yahoo Finance.";
    }

    private static string FmtPrice(StockEntry e) =>
        e.Symbol == "^DJI" ? $"{e.Price:N0}" : $"{e.Price:N2}";
}
