namespace GdeltSearchUI;

public sealed partial class SearchForm : Form
{
    // ── Controls ─────────────────────────────────────────────────────────────
    private TextBox _queryBox = null!;
    private ComboBox _timespanBox = null!;
    private ComboBox _modeBox = null!;
    private CheckBox _titleOnlyBox = null!;
    private Button _searchButton = null!;
    private DataGridView _grid = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripProgressBar _progress = null!;

    private readonly GdeltApiClient _client = new();
    private readonly BlueskyPoster _poster = new();
    private readonly System.Windows.Forms.Timer _autoRefreshTimer = new() { Interval = SearchConstants.AutoRefreshMs };
    private CancellationTokenSource? _cts;
    private Font? _underlineFont;

    private static readonly (string Label, int Hours)[] Timespans =
    [
        ("1 hour",    1),
        ("3 hours",   3),
        ("6 hours",   6),
        ("12 hours", 12),
        ("24 hours", 24),
    ];

    private static readonly (string Label, string Value)[] Modes =
    [
        ("Article List",  "ArtList"),
        ("Article + Geo", "ArtGeo"),
    ];

    public SearchForm(string? initialQuery = null, int defaultTimespanIndex = 0)
    {
        Text = "GDELT Article Search";
        Size = new Size(737, 456);
        MinimumSize = new Size(536, 335);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = DarkTheme.Background;

        Controls.Add(BuildGrid());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusBar());

        if (defaultTimespanIndex > 0 && defaultTimespanIndex < Timespans.Length)
            _timespanBox.SelectedIndex = defaultTimespanIndex;

        _autoRefreshTimer.Tick += async (_, _) => await SearchAsync();

        if (initialQuery is not null)
            Shown += async (_, _) => await LaunchPresetSearchAsync(initialQuery);
        else
            SetStatus("Enter a query and press Search.");
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        _searchButton.Enabled = !busy;
        _searchButton.Text = busy ? "Searching…" : "Search";
        _progress.Visible = busy;
        if (busy) _progress.Style = ProgressBarStyle.Marquee;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    private void HandleRateLimit()
    {
        var backoff = DateTime.Now.AddMinutes(30).ToString("HH:mm");
        SetStatus($"Rate limited by GDELT — retrying at {backoff}.");
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Interval = SearchConstants.RateLimitBackoffMs;
        _autoRefreshTimer.Start();
    }

    private void FinishSearch(int count, bool fromCache)
    {
        var cached = fromCache ? " (cached)" : "";
        var nextRefresh = DateTime.Now.AddMilliseconds(_autoRefreshTimer.Interval).ToString("HH:mm");
        SetStatus($"{count} article{(count == 1 ? "" : "s")} found{cached} — next refresh at {nextRefresh}.");
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Interval = SearchConstants.AutoRefreshMs;
        _autoRefreshTimer.Start();
    }

    // Returns null when an error was handled (caller should bail out).
    private async Task<GdeltSearchResult?> TryFetchAsync(Func<Task<GdeltSearchResult>> fetch)
    {
        try   { return await fetch(); }
        catch (OperationCanceledException)                                                                 { if (!IsDisposed) SetStatus("Search cancelled."); return null; }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests) { if (!IsDisposed) HandleRateLimit(); return null; }
        catch (HttpRequestException ex)                                                                    { if (!IsDisposed) { SetStatus("Network error — see details."); ErrorDialog.Show(this, $"Network error: {ex.Message}"); } return null; }
        catch (Exception ex)                                                                               { if (!IsDisposed) { SetStatus("Error — see details."); ErrorDialog.Show(this, ex.ToString()); } return null; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _underlineFont?.Dispose(); _cts?.Dispose(); _client.Dispose(); _poster.Dispose(); _autoRefreshTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
