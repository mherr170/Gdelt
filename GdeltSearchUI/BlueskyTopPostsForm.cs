namespace GdeltSearchUI;

internal sealed partial class BlueskyTopPostsForm : Form
{
    private readonly BskySortMode _sortMode;
    private List<BskyPost> _posts = [];

    private TextBox      _handleBox   = null!;
    private Button       _fetchBtn    = null!;
    private DataGridView _grid        = null!;
    private Label        _statusLabel = null!;
    private CancellationTokenSource? _cts;

    public BlueskyTopPostsForm(BskySortMode sortMode)
    {
        _sortMode = sortMode;
        BuildLayout();

        // Pre-fill with the stored main Bluesky handle if available.
        var cred = CredentialManager.Load();
        if (cred.HasValue)
            _handleBox.Text = cred.Value.Handle;
    }

    private async Task FetchAsync()
    {
        var handle = _handleBox.Text.Trim().TrimStart('@');
        if (string.IsNullOrEmpty(handle))
        {
            SetStatus("Enter a Bluesky handle to analyze.");
            return;
        }

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
        SetStatus($"Authenticating…");
        _grid.Rows.Clear();

        try
        {
            using var client = new BlueskyAnalyticsClient();
            await client.AuthenticateAsync(authCred.Value.Handle, authCred.Value.Password, ct);
            SetStatus($"Fetching posts for @{handle}…");
            var posts = await client.GetAuthorPostsAsync(handle, maxPosts: 100, ct: ct);

            _posts = _sortMode switch
            {
                BskySortMode.Likes   => [.. posts.OrderByDescending(p => p.LikeCount)],
                BskySortMode.Reposts => [.. posts.OrderByDescending(p => p.RepostCount)],
                BskySortMode.Replies => [.. posts.OrderByDescending(p => p.ReplyCount)],
                BskySortMode.Quotes  => [.. posts.OrderByDescending(p => p.QuoteCount)],
                _                    => posts,
            };

            PopulateGrid();
            SetStatus($"{_posts.Count} posts for @{handle} — sorted by {_sortMode}. Double-click to open in browser.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Cancelled.");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var post in _posts)
        {
            var preview = post.Record.Text.Replace('\n', ' ');
            if (preview.Length > 90) preview = preview[..89] + "…";
            var date = post.CreatedAtLocal == DateTime.MinValue
                ? "—"
                : post.CreatedAtLocal.ToString("MM/dd HH:mm");

            _grid.Rows.Add(date, preview, post.LikeCount, post.RepostCount, post.ReplyCount, post.QuoteCount);
        }
    }

    private void OnRowDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _posts.Count) return;
        var url = _posts[e.RowIndex].BskyUrl;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void SetBusy(bool busy)
    {
        _fetchBtn.Enabled  = !busy;
        _handleBox.Enabled = !busy;
        UseWaitCursor      = busy;
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
