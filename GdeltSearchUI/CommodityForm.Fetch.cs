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
        SetStatus("Fetching EIA energy spot prices…");

        CommodityData result;
        using (var client = new CommodityApiClient(apiKey))
        {
            try   { result = await client.GetAllAsync(); }
            catch (Exception ex) { ShowError(ex.Message); SetBusy(false); return; }
        }

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage!);
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

        // ── OilPriceAPI (optional — only if key is configured) ────────────────
        string? oilPriceStatus = null;
        var oilKey = CredentialManager.LoadOilPriceApiKey();
        if (oilKey is not null)
        {
            SetStatus("Fetching OilPriceAPI live prices…");
            try
            {
                using var oilClient = new OilPriceApiClient(oilKey);
                var oilPrices = await oilClient.GetLatestAsync();
                _lastResult = _lastResult with { OilPrices = oilPrices };

                var oilCatalog = OilPriceApiClient.Catalog;
                for (var j = 0; j < oilCatalog.Length; j++)
                {
                    var entry = oilPrices.FirstOrDefault(p => p.Code == oilCatalog[j].Code);
                    if (entry is null) continue;
                    _oilPriceLabels[j].Text = FmtOilPrice(entry);
                    // delta: no previous price available from this endpoint
                    _oilPriceDeltaLabels[j].Text = "";
                }

                var freshest = oilPrices.Count > 0
                    ? oilPrices.Max(p => p.UpdatedAt)
                    : default;
                oilPriceStatus = freshest != default
                    ? $"OilPrice API as of {freshest:HH:mm}"
                    : "OilPrice API: ok";
            }
            catch (Exception ex)
            {
                AppLogger.Log($"OilPriceAPI fetch failed: {ex.Message}");
                oilPriceStatus = "OilPrice API: error";
            }
        }
        else
        {
            oilPriceStatus = "OilPrice API: no key";
        }

        // ── Status bar ────────────────────────────────────────────────────────
        var dailyDate  = result.Prices.Where(p => !p.Unit.Contains("wk")).Select(p => p.UpdatedAt).Max();
        var weeklyDate = result.Prices.Where(p =>  p.Unit.Contains("wk")).Select(p => p.UpdatedAt).DefaultIfEmpty().Max();
        var statusParts = new List<string> { $"EIA Crude/NG as of {dailyDate:yyyy-MM-dd}" };
        if (weeklyDate != default) statusParts.Add($"Htg Oil/RBOB as of {weeklyDate:yyyy-MM-dd} (wk)");
        if (oilPriceStatus is not null) statusParts.Add(oilPriceStatus);
        SetStatus(string.Join("  ·  ", statusParts));
        SetBusy(false);
    }

}
