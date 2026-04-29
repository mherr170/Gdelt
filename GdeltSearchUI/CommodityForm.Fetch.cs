namespace GdeltSearchUI;

internal partial class CommodityForm
{
    // Called on form shown — fetches both sources sequentially.
    private async Task FetchAllAsync()
    {
        await FetchEiaAsync();
        await FetchYahooAsync();
    }

    // ── EIA (called by EIA Refresh button) ───────────────────────────────────

    private async Task FetchEiaAsync()
    {
        var apiKey = CredentialManager.LoadEiaApiKey();
        if (apiKey is null)
        {
            apiKey = PromptForApiKey("Enter your free EIA API key (eia.gov/opendata):");
            if (apiKey is null) { SetStatus("No EIA API key — fetch cancelled."); return; }
            CredentialManager.SaveEiaApiKey(apiKey);
        }

        SetEiaBusy(true);
        ClearEiaPrices();
        _eiaStatusLabel.Text = "Fetching…";

        CommodityData result;
        using (var client = new CommodityApiClient(apiKey))
        {
            try   { result = await client.GetAllAsync(); }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                _eiaStatusLabel.Text = "Error — see details";
                SetEiaBusy(false);
                return;
            }
        }

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage!);
            _eiaStatusLabel.Text = "Error — see details";
            SetEiaBusy(false);
            return;
        }

        // Preserve any Yahoo prices already in _lastResult
        _lastResult = (_lastResult is null)
            ? result
            : result with { OilPrices = _lastResult.OilPrices };

        var catalog = CommodityApiClient.Catalog;
        for (var i = 0; i < catalog.Length; i++)
        {
            var match = result.Prices.FirstOrDefault(p => p.Slug == catalog[i].Slug);
            if (match is null) continue;
            _priceLabels[i].Text = FmtPrice(match);
            var (text, color) = FormatDelta(match.Price, match.Previous);
            _deltaLabels[i].Text      = text;
            _deltaLabels[i].ForeColor = color;
        }

        UpdatePostButton();

        var dailyDate  = result.Prices.Where(p => !p.Unit.Contains("wk")).Select(p => p.UpdatedAt).Max();
        var weeklyDate = result.Prices.Where(p =>  p.Unit.Contains("wk")).Select(p => p.UpdatedAt).DefaultIfEmpty().Max();
        _eiaStatusLabel.Text = weeklyDate != default
            ? $"Crude/NG as of {dailyDate:yyyy-MM-dd}  ·  Htg Oil/RBOB as of {weeklyDate:yyyy-MM-dd} (wk)"
            : $"As of {dailyDate:yyyy-MM-dd}";

        SetEiaBusy(false);
    }

    // ── Yahoo Finance (called by Yahoo Refresh button) ────────────────────────

    private async Task FetchYahooAsync()
    {
        SetYahooBusy(true);
        ClearYahooPrices();
        _yahooStatusLabel.Text = "Fetching…";

        try
        {
            using var client   = new YahooFinanceApiClient();
            var livePrices     = await client.GetLatestAsync();

            if (_lastResult is not null)
                _lastResult = _lastResult with { OilPrices = livePrices };

            var yahooCatalog = YahooFinanceApiClient.Catalog;
            for (var j = 0; j < yahooCatalog.Length; j++)
            {
                var entry = livePrices.FirstOrDefault(p => p.Code == yahooCatalog[j].Code);
                if (entry is null) continue;
                _oilPriceLabels[j].Text = FmtOilPrice(entry);
                if (entry.Previous.HasValue)
                {
                    var (dText, dColor) = FormatDelta(entry.Price, entry.Previous);
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
