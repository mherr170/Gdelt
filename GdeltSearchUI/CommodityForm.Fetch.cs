namespace GdeltSearchUI;

internal partial class CommodityForm
{
    private async Task FetchAsync()
    {
        var apiKey = CredentialManager.LoadEiaApiKey();
        if (apiKey is null)
        {
            apiKey = PromptForApiKey("Enter your free EIA API key (eia.gov/opendata):");
            if (apiKey is null) { SetStatus("No EIA API key — fetch cancelled."); return; }
            CredentialManager.SaveEiaApiKey(apiKey);
        }

        SetBusy(true);
        ClearPrices();
        _eiaStatusLabel.Text   = "Fetching…";
        _yahooStatusLabel.Text = "Fetching…";

        // ── EIA ───────────────────────────────────────────────────────────────
        CommodityData result;
        using (var client = new CommodityApiClient(apiKey))
        {
            try   { result = await client.GetAllAsync(); }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                _eiaStatusLabel.Text = "Error — see details";
                SetBusy(false);
                return;
            }
        }

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage!);
            _eiaStatusLabel.Text = "Error — see details";
            SetBusy(false);
            return;
        }

        _lastResult = result;

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
        var eiaStatus = $"Crude/NG as of {dailyDate:yyyy-MM-dd}";
        if (weeklyDate != default) eiaStatus += $"  ·  Htg Oil/RBOB as of {weeklyDate:yyyy-MM-dd} (wk)";
        _eiaStatusLabel.Text = eiaStatus;

        // ── Yahoo Finance futures (no key required) ───────────────────────────
        try
        {
            using var yahooClient = new YahooFinanceApiClient();
            var livePrices = await yahooClient.GetLatestAsync();
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

        SetBusy(false);
    }

}
