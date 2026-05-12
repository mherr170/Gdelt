namespace GdeltSearchUI;

internal partial class CongressForm
{
    private async Task FetchAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetBusy(true);
        _grid.Rows.Clear();
        UpdatePostButton();

        var apiKey = CredentialManager.LoadProPublicaKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetStatus("No ProPublica API key — click ⚙ API Key to configure.");
            SetBusy(false);
            return;
        }

        SetStatus("Fetching recent votes from House and Senate…");

        List<CongressVote> votes;
        try
        {
            var congress = CongressApiClient.CurrentCongress(DateTime.Now);
            using var client = new CongressApiClient(apiKey);
            var houseTask  = client.GetRecentHouseVotesAsync(congress, _cts.Token);
            var senateTask = client.GetRecentSenateVotesAsync(congress, _cts.Token);
            await Task.WhenAll(houseTask, senateTask);
            votes = houseTask.Result.Concat(senateTask.Result)
                .OrderByDescending(v => v.VoteTime)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
            SetBusy(false);
            return;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SetBusy(false);
            return;
        }

        foreach (var v in votes)
        {
            var chamberShort = v.Chamber.Equals("Senate", StringComparison.OrdinalIgnoreCase) ? "Senate" : "House";
            var dateStr      = v.VoteTime != DateTime.MinValue ? v.VoteTime.ToString("MMM d") : "—";
            var tallyStr     = $"{v.Yes} / {v.No}";
            var demStr       = $"✓{v.DemYes} ✗{v.DemNo}";
            var repStr       = $"✓{v.RepYes} ✗{v.RepNo}";

            var idx = _grid.Rows.Add(chamberShort, dateStr, v.RollCall, v.DisplayBill, v.Result, tallyStr, demStr, repStr);
            var row = _grid.Rows[idx];
            row.Tag = v;

            if (CongressPostTracker.HasBeenPosted(v.UniqueKey))
                row.DefaultCellStyle.ForeColor = DarkTheme.TextMuted;

            var resultCell = row.Cells["Result"];
            resultCell.Style.ForeColor = ResultColor(v.Result);
        }

        SetStatus($"{votes.Count} vote{(votes.Count == 1 ? "" : "s")} — {DateTime.Now:HH:mm}");
        SetBusy(false);
    }

    private static Color ResultColor(string result) =>
        result.Contains("Pass", StringComparison.OrdinalIgnoreCase) ||
        result.Contains("Agreed", StringComparison.OrdinalIgnoreCase) ||
        result.Contains("Confirmed", StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb(0x4F, 0xB5, 0x6E)
            : result.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
              result.Contains("Rejected", StringComparison.OrdinalIgnoreCase) ||
              result.Contains("Defeated", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(0xFF, 0x66, 0x66)
                : DarkTheme.TextPrimary;
}
