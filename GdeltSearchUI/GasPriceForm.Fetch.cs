namespace GdeltSearchUI;

internal partial class GasPriceForm
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

        NationalGasPrices result;
        using (var client = new GasPriceApiClient(apiKey))
        {
            try   { result = await client.GetNationalAveragesAsync(); }
            catch (Exception ex) { ShowError(ex.Message); SetBusy(false); return; }
        }

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage!);
            SetBusy(false);
            return;
        }

        _lastResult = result;
        _regularLabel.Text  = Fmt(result.Regular);
        _midGradeLabel.Text = Fmt(result.MidGrade);
        _premiumLabel.Text  = Fmt(result.Premium);
        _dieselLabel.Text   = Fmt(result.Diesel);
        ApplyDelta(_regularDelta,  result.Regular,  result.Previous?.Regular);
        ApplyDelta(_midGradeDelta, result.MidGrade, result.Previous?.MidGrade);
        ApplyDelta(_premiumDelta,  result.Premium,  result.Previous?.Premium);
        ApplyDelta(_dieselDelta,   result.Diesel,   result.Previous?.Diesel);
        UpdatePostButton();

        var status = $"Week of {result.Period}  ·  source: EIA";
        if (result.Previous is { Period.Length: > 0 } prev)
            status += $"  ·  vs {prev.Period}";
        SetStatus(status);
        SetBusy(false);
    }

}
