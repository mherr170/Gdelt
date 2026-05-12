namespace GdeltSearchUI;

internal partial class CongressForm : DataForm
{
    private Button           _refreshBtn   = null!;
    private Button           _postBtn      = null!;
    private DataGridView     _grid         = null!;
    private ToolStripStatusLabel  _statusLabel = null!;
    private ToolStripProgressBar  _progress    = null!;

    private readonly BlueskyPoster _poster = new();
    private CancellationTokenSource? _cts;

    public CongressForm()
    {
        Text            = "Congress Votes — ProPublica";
        Size            = new Size(900, 500);
        MinimumSize     = new Size(700, 380);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = DarkTheme.Background;
        FormBorderStyle = FormBorderStyle.Sizable;

        Controls.Add(BuildGrid());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusBar());

        Shown += async (_, _) => await FetchAsync();
    }

    private void SetBusy(bool busy)
    {
        _refreshBtn.Enabled = !busy;
        _refreshBtn.Text    = busy ? "…" : "Refresh";
        _progress.Visible   = busy;
        if (busy) _progress.Style = ProgressBarStyle.Marquee;
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    private void UpdatePostButton()
    {
        if (_grid.CurrentRow?.Tag is not CongressVote vote)
        {
            _postBtn.Enabled  = false;
            _postBtn.Text     = "Post Selected";
            _postBtn.BackColor = DarkTheme.PostButtonDefault;
            return;
        }

        var posted = CongressPostTracker.HasBeenPosted(vote.UniqueKey);
        _postBtn.Enabled   = true;
        _postBtn.Text      = posted ? $"✓ Posted" : "Post Selected";
        _postBtn.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poster.Dispose(); _cts?.Dispose(); }
        base.Dispose(disposing);
    }
}
