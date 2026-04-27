namespace GdeltSearchUI;

internal partial class GasPriceForm : DataForm
{
    private Button           _refreshButton = null!;
    private Button           _postButton    = null!;
    private Label            _regularLabel  = null!;
    private Label            _midGradeLabel = null!;
    private Label            _premiumLabel  = null!;
    private Label            _dieselLabel   = null!;
    private Label            _statusLabel   = null!;
    private NationalGasPrices? _lastResult;

    private readonly BlueskyPoster _poster = new();

    public GasPriceForm()
    {
        Text = "US Gas Prices — National Average";
        Size = new Size(480, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = DarkTheme.Background;

        Controls.Add(BuildPricePanel());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusLabel());

        Shown += async (_, _) => await FetchAsync();
    }

    private static string Fmt(double? v) => v.HasValue ? $"${v.Value:F3}" : "N/A";

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _refreshButton.Text    = busy ? "…" : "Refresh";
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    private void ClearPrices()
    {
        _lastResult         = null;
        _postButton.Enabled = false;
        _regularLabel.Text  = "—";
        _midGradeLabel.Text = "—";
        _premiumLabel.Text  = "—";
        _dieselLabel.Text   = "—";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poster.Dispose();
        base.Dispose(disposing);
    }
}
