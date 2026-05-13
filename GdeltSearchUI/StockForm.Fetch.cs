namespace GdeltSearchUI;

internal partial class StockForm
{
    private async Task FetchAsync()
    {
        SetBusy(true);
        _grid.Rows.Clear();
        _tradingDate = null;
        UpdatePostButton();
        SetStatus("Fetching index data from Yahoo Finance…");

        IReadOnlyList<StockEntry> entries;
        try
        {
            using var client = new StockApiClient();
            entries = await client.GetLatestAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SetBusy(false);
            return;
        }

        _lastEntries = entries;

        foreach (var e in entries)
        {
            var priceStr  = e.Symbol == "^DJI" ? $"{e.Price:N0}" : $"{e.Price:N2}";
            var prevStr   = e.Symbol == "^DJI" ? $"{e.PreviousClose:N0}" : $"{e.PreviousClose:N2}";
            var changeStr = $"{e.ChangePercent:+0.00;-0.00}%";
            var timeStr   = e.UpdatedAt.ToLocalTime().ToString("h:mm tt");

            var idx = _grid.Rows.Add(e.DisplayName, priceStr, changeStr, prevStr, timeStr);
            var row = _grid.Rows[idx];
            row.Tag = e;

            var changeCell = row.Cells["Change"];
            changeCell.Style.ForeColor = e.ChangePercent > 0
                ? DarkTheme.DeltaUp
                : e.ChangePercent < 0 ? DarkTheme.DeltaDown : DarkTheme.TextMuted;
            changeCell.Style.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        }

        _tradingDate = StockApiClient.TradingDateIfClosed(entries);
        UpdatePostButton();

        // Render chart on background thread
        if (entries.Any(e => e.Intraday.Count > 1))
        {
            SetStatus("Rendering intraday chart…");
            try
            {
                var png = await Task.Run(() => StockChartGenerator.RenderPng(entries));
                if (png.Length > 0)
                {
                    var ms  = new MemoryStream(png);
                    var img = System.Drawing.Image.FromStream(ms);
                    var old = _chartBox.Image;
                    _chartBox.Image = img;
                    old?.Dispose();
                }
            }
            catch { /* chart is a nice-to-have */ }
        }

        var statusSuffix = _tradingDate is not null
            ? $" — market closed {_tradingDate}"
            : " — market open or non-trading day";
        SetStatus($"{entries.Count} index value{(entries.Count == 1 ? "" : "s")} loaded{statusSuffix}");
        SetBusy(false);
    }
}
