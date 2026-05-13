namespace GdeltSearchUI;

internal sealed partial class BlueskyMostFollowedForm : Form
{
    private TextBox      _queryBox    = null!;
    private Button       _fetchBtn    = null!;
    private DataGridView _grid        = null!;
    private Label        _statusLabel = null!;
    private List<BskyProfile> _profiles = [];
    private CancellationTokenSource? _cts;

    public BlueskyMostFollowedForm()
    {
        BuildLayout();
    }

    private async Task FetchAsync()
    {
        var query = _queryBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) { SetStatus("Enter a topic to search."); return; }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var authCred = CredentialManager.Load();
        if (authCred is null)
        {
            SetStatus("No Bluesky account configured — set one up in the main search settings.");
            SetBusy(false);
            return;
        }

        SetBusy(true);
        SetStatus("Authenticating…");
        _grid.Rows.Clear();

        try
        {
            using var client = new BlueskyAnalyticsClient();
            await client.AuthenticateAsync(authCred.Value.Handle, authCred.Value.Password, ct);

            SetStatus("Finding accounts…");
            var actors = await client.SearchActorsAsync(query, limit: 25, ct: ct);
            if (actors.Count == 0)
            {
                SetStatus("No accounts found.");
                return;
            }

            SetStatus($"Loading detailed profiles for {actors.Count} accounts…");
            _profiles = await client.GetProfilesAsync(actors.Select(a => a.Handle), ct);
            _profiles.Sort((a, b) => b.FollowersCount.CompareTo(a.FollowersCount));

            PopulateGrid();
            SetStatus($"{_profiles.Count} accounts for \"{query}\" — sorted by followers. Double-click to open profile.");
        }
        catch (OperationCanceledException) { SetStatus("Cancelled."); }
        catch (Exception ex)              { SetStatus($"Error: {ex.Message}"); }
        finally                            { SetBusy(false); }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        for (int i = 0; i < _profiles.Count; i++)
        {
            var p = _profiles[i];
            _grid.Rows.Add(i + 1, p.Handle, p.DisplayName, p.FollowersCount, p.PostsCount);
        }
    }

    private void OnRowDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _profiles.Count) return;
        var url = $"https://bsky.app/profile/{_profiles[e.RowIndex].Handle}";
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private void SetBusy(bool busy)
    {
        _fetchBtn.Enabled = !busy;
        _queryBox.Enabled = !busy;
        UseWaitCursor     = busy;
    }

    private void SetStatus(string msg)
    {
        if (InvokeRequired) Invoke(() => _statusLabel.Text = msg);
        else _statusLabel.Text = msg;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnFormClosed(e);
    }
}
