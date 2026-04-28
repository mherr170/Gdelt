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

        var dailyDate  = result.Prices.Where(p => !p.Unit.Contains("wk")).Select(p => p.UpdatedAt).Max();
        var weeklyDate = result.Prices.Where(p =>  p.Unit.Contains("wk")).Select(p => p.UpdatedAt).DefaultIfEmpty().Max();
        var statusParts = new List<string> { $"Crude/NG as of {dailyDate:yyyy-MM-dd}" };
        if (weeklyDate != default) statusParts.Add($"Htg Oil/RBOB as of {weeklyDate:yyyy-MM-dd} (wk)");
        SetStatus(string.Join("  ·  ", statusParts));
        SetBusy(false);
    }

}
