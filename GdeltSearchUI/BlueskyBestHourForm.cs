namespace GdeltSearchUI;

internal sealed partial class BlueskyBestHourForm : Form
{
    private TextBox      _handleBox   = null!;
    private Button       _fetchBtn    = null!;
    private DataGridView _grid        = null!;
    private Label        _statusLabel = null!;
    private CancellationTokenSource? _cts;

    private record HourBucket(
        int    Hour,
        int    PostCount,
        double AvgLikes,
        double AvgReposts,
        double AvgEngagement);

    private List<HourBucket> _buckets = [];

    public BlueskyBestHourForm()
    {
        BuildLayout();
        var cred = CredentialManager.Load();
        if (cred.HasValue)
            _handleBox.Text = cred.Value.Handle;
    }

    private async Task FetchAsync()
    {
        var handle = _handleBox.Text.Trim().TrimStart('@');
        if (string.IsNullOrEmpty(handle)) { SetStatus("Enter a handle."); return; }

        var authCred = CredentialManager.Load();
        if (authCred is null)
        {
            SetStatus("No Bluesky account configured — set one up in the main search settings.");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true);
        SetStatus("Authenticating…");
        _grid.Rows.Clear();

        try
        {
            using var client = new BlueskyAnalyticsClient();
            await client.AuthenticateAsync(authCred.Value.Handle, authCred.Value.Password, ct);

            SetStatus($"Fetching posts for @{handle}…");
            var posts = await client.GetAuthorPostsAsync(handle, maxPosts: 100, ct: ct);

            if (posts.Count == 0)
            {
                SetStatus("No posts found for this account.");
                return;
            }

            // Group into 24 hour buckets.
            var grouped = posts
                .Where(p => p.CreatedAtLocal != DateTime.MinValue)
                .GroupBy(p => p.CreatedAtLocal.Hour)
                .ToDictionary(g => g.Key, g => g.ToList());

            _buckets = Enumerable.Range(0, 24).Select(h =>
            {
                if (!grouped.TryGetValue(h, out var bucket) || bucket.Count == 0)
                    return new HourBucket(h, 0, 0, 0, 0);

                double avgLikes    = bucket.Average(p => p.LikeCount);
                double avgReposts  = bucket.Average(p => p.RepostCount);
                double avgEngage   = bucket.Average(p => p.LikeCount + p.RepostCount + p.ReplyCount);
                return new HourBucket(h, bucket.Count, avgLikes, avgReposts, avgEngage);
            })
            .OrderByDescending(b => b.AvgEngagement)
            .ToList();

            double maxEngage = _buckets.Max(b => b.AvgEngagement);
            PopulateGrid(maxEngage);

            // Warn if results look like a scheduled bot (one hour dominates).
            int topCount    = _buckets[0].PostCount;
            int lowSample   = _buckets.Count(b => b.PostCount > 0 && b.PostCount < 3);
            string warning  = topCount >= (int)(posts.Count * 0.7)
                ? " ⚠ Most posts at one hour — may reflect a fixed schedule."
                : lowSample > 8
                    ? " Note: many hours have low sample counts."
                    : "";

            SetStatus($"{posts.Count} posts analysed across {grouped.Count} hour(s).{warning}");
        }
        catch (OperationCanceledException) { SetStatus("Cancelled."); }
        catch (Exception ex)              { SetStatus($"Error: {ex.Message}"); }
        finally                            { SetBusy(false); }
    }

    private void PopulateGrid(double maxEngage)
    {
        _grid.Rows.Clear();
        foreach (var b in _buckets)
        {
            var bar = b.PostCount == 0 || maxEngage == 0
                ? new string('░', 12)
                : MakeBar(b.AvgEngagement, maxEngage);

            _grid.Rows.Add(
                HourLabel(b.Hour),
                b.PostCount == 0 ? "—" : b.PostCount.ToString(),
                b.PostCount == 0 ? "—" : b.AvgLikes.ToString("F1"),
                b.PostCount == 0 ? "—" : b.AvgReposts.ToString("F1"),
                b.PostCount == 0 ? "—" : b.AvgEngagement.ToString("F1"),
                bar);
        }
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _buckets.Count) return;
        var b = _buckets[e.RowIndex];

        if (b.PostCount == 0)
        {
            e.CellStyle.ForeColor = DarkTheme.TextMuted;
        }
        else if (e.RowIndex == 0)
        {
            // Top-engagement hour — gold highlight.
            e.CellStyle.ForeColor = Color.FromArgb(0xFF, 0xD7, 0x00);
            e.CellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        }
        else if (b.PostCount < 3)
        {
            // Low sample — dim slightly.
            e.CellStyle.ForeColor = Color.FromArgb(0xA0, 0xA0, 0xA0);
        }
    }

    private static string HourLabel(int h) => h switch
    {
        0  => "12 am (mid)",
        12 => "12 pm (noon)",
        < 12 => $"{h} am",
        _    => $"{h - 12} pm",
    };

    private static string MakeBar(double value, double maxValue)
    {
        int filled = (int)Math.Round(12.0 * value / maxValue);
        filled = Math.Clamp(filled, 0, 12);
        return new string('█', filled) + new string('░', 12 - filled);
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
