namespace GdeltSearchUI;

internal sealed partial class BlueskyBuzzCompareForm : Form
{
    private TextBox[] _keywordBoxes = null!;
    private Button    _compareBtn   = null!;
    private DataGridView _grid      = null!;
    private Label     _statusLabel  = null!;
    private CancellationTokenSource? _cts;

    private record BuzzResult(
        string Keyword, int PostCount, int TotalLikes, int TotalReposts, int TotalReplies);

    public BlueskyBuzzCompareForm()
    {
        BuildLayout();
    }

    private async Task CompareAsync()
    {
        var keywords = _keywordBoxes
            .Select(b => b.Text.Trim())
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToArray();

        if (keywords.Length == 0) { SetStatus("Enter at least one keyword."); return; }

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

        var results = new List<BuzzResult>();

        try
        {
            using var client = new BlueskyAnalyticsClient();
            await client.AuthenticateAsync(authCred.Value.Handle, authCred.Value.Password, ct);

            foreach (var kw in keywords)
            {
                SetStatus($"Searching \"{kw}\"…");
                var posts = await client.SearchPostsAsync(kw, limit: 100, ct: ct);
                results.Add(new BuzzResult(
                    Keyword:      kw,
                    PostCount:    posts.Count,
                    TotalLikes:   posts.Sum(p => p.LikeCount),
                    TotalReposts: posts.Sum(p => p.RepostCount),
                    TotalReplies: posts.Sum(p => p.ReplyCount)));
            }

            results.Sort((a, b) => b.TotalLikes.CompareTo(a.TotalLikes));

            int maxLikes = results.Max(r => r.TotalLikes);
            foreach (var r in results)
            {
                var avg = r.PostCount > 0 ? (double)r.TotalLikes / r.PostCount : 0;
                _grid.Rows.Add(
                    r.Keyword,
                    r.PostCount,
                    r.TotalLikes,
                    r.TotalReposts,
                    avg.ToString("F1"),
                    MakeBar(r.TotalLikes, maxLikes));
            }

            SetStatus($"Compared {results.Count} keyword(s). Sorted by total likes.");
        }
        catch (OperationCanceledException) { SetStatus("Cancelled."); }
        catch (Exception ex)              { SetStatus($"Error: {ex.Message}"); }
        finally                            { SetBusy(false); }
    }

    private static string MakeBar(int value, int maxValue)
    {
        if (maxValue == 0) return new string('░', 12);
        int filled = (int)Math.Round(12.0 * value / maxValue);
        filled = Math.Clamp(filled, 0, 12);
        return new string('█', filled) + new string('░', 12 - filled);
    }

    private void SetBusy(bool busy)
    {
        _compareBtn.Enabled = !busy;
        foreach (var b in _keywordBoxes) b.Enabled = !busy;
        UseWaitCursor = busy;
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
