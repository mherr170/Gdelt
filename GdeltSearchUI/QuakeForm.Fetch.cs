namespace GdeltSearchUI;

internal partial class QuakeForm
{
    private async Task FetchAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetBusy(true);
        _grid.Rows.Clear();
        _postBtn.Enabled = false;

        var minMag = MagFilters[_magBox.SelectedIndex].MinMag;
        var hours  = TimeRanges[_timeBox.SelectedIndex].Hours;

        List<QuakeEvent> events;
        string? error;
        using (var client = new QuakeApiClient())
        {
            try   { (events, error) = await client.GetRecentAsync(minMag, hours, _cts.Token); }
            catch (OperationCanceledException) { SetStatus("Cancelled."); SetBusy(false); return; }
            catch (Exception ex)               { ShowError(ex.Message); SetBusy(false); return; }
        }

        if (error is not null) { ShowError(error); SetBusy(false); return; }

        foreach (var q in events)
        {
            var magStr    = q.Magnitude.ToString("F1");
            var depthStr  = q.DepthKm.HasValue ? q.DepthKm.Value.ToString("F1") : "—";
            var timeStr   = q.Time.ToString("MMM d  HH:mm");
            var tsunamiStr = q.TsunamiWarning ? "YES" : "";

            var row = _grid.Rows[_grid.Rows.Add(magStr, q.Place, depthStr, timeStr, tsunamiStr)];
            row.Tag = q;

            // Colour-code by magnitude
            var magCell = row.Cells["Mag"];
            magCell.Style.ForeColor = MagColor(q.Magnitude);

            if (q.TsunamiWarning)
                row.Cells["Tsunami"].Style.ForeColor = Color.FromArgb(0xFF, 0x55, 0x55);
        }

        SetStatus($"{events.Count} event{(events.Count == 1 ? "" : "s")}  ·  M{minMag}+  ·  past {(hours < 48 ? $"{hours}h" : $"{hours / 24}d")}  ·  {DateTime.Now:HH:mm}");
        SetBusy(false);
    }

    private async Task AutoPostAsync()
    {
        var creds = CredentialManager.LoadQuakeBluesky();
        if (creds is null)
        {
            SetStatus("Auto-post: no Bluesky account configured — use ⚙ Account.");
            return;
        }

        List<QuakeEvent> events;
        using (var client = new QuakeApiClient())
        {
            var (e, err) = await client.GetRecentAsync(minMagnitude: 5.0, hours: 2);
            if (err is not null || e.Count == 0) return;
            events = e;
        }

        var unseen = events.Where(q => q.Id.Length > 0 && !QuakePostTracker.HasBeenPosted(q.Id)).ToList();
        if (unseen.Count == 0)
        {
            SetStatus($"Auto-post: no new M5+ events  ·  checked {DateTime.Now:HH:mm}");
            return;
        }

        foreach (var quake in unseen)
        {
            SetStatus($"Auto-posting M{quake.Magnitude:F1} — {quake.Place}…");
            var (headline, body, tags) = await LmStudioPostGenerator.GenerateQuakePostAsync(quake);
            var text = BuildPostText(quake, headline, body, tags);
            var (ok, error) = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);

            if (ok)
            {
                QuakePostTracker.MarkPosted(quake.Id);
                AppLogger.Log($"Auto-posted quake {quake.Id}: M{quake.Magnitude:F1} {quake.Place}");
                SetStatus($"Auto-posted M{quake.Magnitude:F1} — {quake.Place}  ·  {DateTime.Now:HH:mm}");
            }
            else
            {
                AppLogger.Log($"Auto-post failed for {quake.Id}: {error}");
            }

            await Task.Delay(2000);
        }
    }

    private static Color MagColor(double mag) => mag switch
    {
        >= 7.0 => Color.FromArgb(0xFF, 0x55, 0x55),
        >= 6.0 => Color.FromArgb(0xFF, 0xA5, 0x00),
        >= 5.0 => Color.FromArgb(0xFF, 0xD7, 0x00),
        _      => DarkTheme.TextPrimary,
    };
}
