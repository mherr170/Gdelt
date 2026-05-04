namespace GdeltSearchUI;

internal partial class CommodityForm
{
    internal async Task FetchYahooAsync()
    {
        SetYahooBusy(true);
        ClearYahooPrices();
        _yahooStatusLabel.Text = "Fetching…";

        try
        {
            IReadOnlyList<OilPriceEntry> livePrices;
            using (var client = new YahooFinanceApiClient())
                livePrices = await client.GetLatestAsync();

            _yahooData = livePrices;

            var lastPostPrices = YahooPriceCache.Load().LastOrDefault()?.Prices;
            var catalog        = YahooFinanceApiClient.Catalog;

            for (var j = 0; j < catalog.Length; j++)
            {
                var entry = livePrices.FirstOrDefault(p => p.Code == catalog[j].Code);
                if (entry is null) continue;
                _oilPriceLabels[j].Text = FmtOilPrice(entry);

                double? baseline = lastPostPrices is not null &&
                                   lastPostPrices.TryGetValue(catalog[j].Code, out var lp) && lp != 0
                    ? lp
                    : entry.Previous;

                if (baseline.HasValue)
                {
                    var (dText, dColor) = FormatDelta(entry.Price, baseline);
                    _oilPriceDeltaLabels[j].Text      = dText;
                    _oilPriceDeltaLabels[j].ForeColor = dColor;
                }
            }

            UpdateYahooPostButton();

            var freshest = livePrices.Count > 0 ? livePrices.Max(p => p.UpdatedAt) : default;
            _yahooStatusLabel.Text = freshest != default
                ? $"Last updated {freshest.LocalDateTime:HH:mm}  ·  ~15 min delayed"
                : "Data received";
        }
        catch (Exception ex)
        {
            AppLogger.Log($"Yahoo Finance fetch failed: {ex.Message}");
            _yahooStatusLabel.Text = "Error fetching futures — see log";
        }

        SetYahooBusy(false);
    }
}
