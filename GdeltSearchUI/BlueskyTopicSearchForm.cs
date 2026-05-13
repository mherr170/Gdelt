namespace GdeltSearchUI;

internal sealed partial class BlueskyTopicSearchForm : Form
{
    private TextBox      _queryBox    = null!;
    private Button       _fetchBtn    = null!;
    private DataGridView _grid        = null!;
    private Label        _statusLabel = null!;
    private List<BskyPost> _posts    = [];
    private CancellationTokenSource? _cts;

    public BlueskyTopicSearchForm(string initialQuery = "")
    {
        BuildLayout();
        if (!string.IsNullOrEmpty(initialQuery))
            _queryBox.Text = initialQuery;
    }

    private async Task FetchAsync()
    {
        var query = _queryBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) { SetStatus("Enter a search query."); return; }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

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
            await client.AuthenticateAsync(authCred.Value.Handle, authCred.Value.Password, _cts.Token);
            SetStatus($"Searching Bluesky for \"{query}\"…");
            _posts = await client.SearchPostsAsync(query, limit: 100, ct: _cts.Token);
            PopulateGrid();
            SetStatus($"{_posts.Count} posts found for \"{query}\". Double-click to open in browser.");
        }
        catch (OperationCanceledException) { SetStatus("Cancelled."); }
        catch (Exception ex)              { SetStatus($"Error: {ex.Message}"); }
        finally                            { SetBusy(false); }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var post in _posts)
        {
            var preview = post.Record.Text.Replace('\n', ' ');
            if (preview.Length > 90) preview = preview[..89] + "…";
            var date    = post.CreatedAtLocal == DateTime.MinValue ? "—" : post.CreatedAtLocal.ToString("MM/dd HH:mm");
            var author  = post.Author.Handle;
            _grid.Rows.Add(date, author, preview, post.LikeCount, post.RepostCount, post.ReplyCount);
        }
    }

    private void OnRowDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _posts.Count) return;
        OpenUrl(_posts[e.RowIndex].BskyUrl);
    }

    private static void OpenUrl(string url)
    {
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
