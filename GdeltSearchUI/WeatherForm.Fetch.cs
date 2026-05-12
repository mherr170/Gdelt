namespace GdeltSearchUI;

internal partial class WeatherForm
{
    private async Task FetchAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetBusy(true);
        _grid.Rows.Clear();
        _postBtn.Enabled = false;
        SetStatus("Fetching active alerts from api.weather.gov…");

        IReadOnlyList<WeatherAlert> alerts;
        try
        {
            using var client = new WeatherApiClient();
            alerts = await client.GetActiveAlertsAsync(_cts.Token);
        }
        catch (OperationCanceledException) { SetStatus("Cancelled."); SetBusy(false); return; }
        catch (Exception ex)              { ShowError(ex.Message);    SetBusy(false); return; }

        foreach (var a in alerts)
        {
            var expiresStr = a.Expires.HasValue ? a.Expires.Value.ToString("h:mm tt") : "—";
            var sender     = a.SenderName.Replace("National Weather Service ", "NWS ");
            var area       = a.AreaDesc.Length > 80 ? a.AreaDesc[..79] + "…" : a.AreaDesc;

            var idx = _grid.Rows.Add(a.Event, a.Severity, area, expiresStr, sender);
            var row = _grid.Rows[idx];
            row.Tag = a;

            if (WeatherPostTracker.HasBeenPosted(a.Id))
                row.DefaultCellStyle.ForeColor = DarkTheme.TextMuted;

            row.Cells["Event"].Style.ForeColor = SeverityColor(a.Severity);
            row.Cells["Severity"].Style.ForeColor = SeverityColor(a.Severity);
        }

        var newCount = alerts.Count(a => !WeatherPostTracker.HasBeenPosted(a.Id));
        SetStatus($"{alerts.Count} active alert{(alerts.Count == 1 ? "" : "s")} — {newCount} unposted — {DateTime.Now:HH:mm}");
        SetBusy(false);
    }

    private static Color SeverityColor(string severity) => severity switch
    {
        var s when s.Equals("Extreme", StringComparison.OrdinalIgnoreCase)  => Color.FromArgb(0xFF, 0x55, 0x55),
        var s when s.Equals("Severe",  StringComparison.OrdinalIgnoreCase)  => Color.FromArgb(0xFF, 0xA5, 0x00),
        _ => DarkTheme.TextPrimary,
    };
}
