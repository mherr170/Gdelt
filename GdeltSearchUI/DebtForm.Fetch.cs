namespace GdeltSearchUI;

internal partial class DebtForm
{
    private async Task FetchAsync()
    {
        SetBusy(true);
        ClearValues();

        NationalDebt result;
        using (var client = new DebtApiClient())
        {
            try   { result = await client.GetLatestAsync(); }
            catch (Exception ex) { ShowError(ex.Message); SetBusy(false); return; }
        }

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage!);
            SetBusy(false);
            return;
        }

        _lastResult = result;
        var c = result.Current!;
        var p = result.Previous;

        _totalLabel.Text    = FmtTrillions(c.TotalPublicDebt);
        _publicLabel.Text   = FmtTrillions(c.DebtHeldByPublic);
        _intragovLabel.Text = FmtTrillions(c.IntragovHoldings);

        var pct = DebtApiClient.PercentChange(result);
        _percentLabel.Text = pct.HasValue ? $"{(pct >= 0 ? "+" : "")}{pct:F4}%" : "N/A";
        _percentLabel.ForeColor = pct switch
        {
            > 0 => DarkTheme.DeltaUp,
            < 0 => DarkTheme.DeltaDown,
            _   => DarkTheme.TextPrimary,
        };

        ApplyDelta(_totalDelta,    c.TotalPublicDebt,  p?.TotalPublicDebt);
        ApplyDelta(_publicDelta,   c.DebtHeldByPublic, p?.DebtHeldByPublic);
        ApplyDelta(_intragovDelta, c.IntragovHoldings, p?.IntragovHoldings);
        _percentDelta.Text = "";

        UpdatePostButton();

        var status = $"As of {c.RecordDate:yyyy-MM-dd}  ·  source: US Treasury";
        if (p is not null) status += $"  ·  vs {p.RecordDate:yyyy-MM-dd}";
        SetStatus(status);
        SetBusy(false);

        await MaybeAutoPostAsync();
    }

    private async Task MaybeAutoPostAsync()
    {
        if (_lastResult?.Current is null) return;

        var date = _lastResult.Current.RecordDate.ToString("yyyy-MM-dd");
        if (DebtPostTracker.HasBeenPosted(date))
        {
            SetStatus($"Already posted for {date} — skipping.");
            return;
        }

        if (CredentialManager.LoadDebtBluesky() is null)
        {
            SetStatus($"Not yet posted for {date} — set up Bluesky account to enable auto-post.");
            return;
        }

        await PostToBlueskyAsync();
    }
}
