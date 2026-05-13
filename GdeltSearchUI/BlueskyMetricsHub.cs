namespace GdeltSearchUI;

internal sealed partial class BlueskyMetricsHub : Form
{
    private Label _statusLabel = null!;

    public BlueskyMetricsHub()
    {
        Text            = "Bluesky Analytics";
        Size            = new Size(580, 616);
        MinimumSize     = new Size(580, 616);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = DarkTheme.Background;

        BuildLayout();
    }

    private void OpenTopPosts(BskySortMode sortMode)
    {
        new BlueskyTopPostsForm(sortMode).Show(this);
    }

    private void SetStatus(string msg)
    {
        if (InvokeRequired) Invoke(() => _statusLabel.Text = msg);
        else _statusLabel.Text = msg;
    }
}
