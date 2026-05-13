namespace GdeltSearchUI;

internal sealed partial class BlueskyProfileForm : Form
{
    private TextBox  _handleBox      = null!;
    private Button   _fetchBtn       = null!;
    private Label    _displayName    = null!;
    private Label    _handleLabel    = null!;
    private RichTextBox _bioBox      = null!;
    private Label    _followersValue = null!;
    private Label    _followingValue = null!;
    private Label    _postsValue     = null!;
    private Label    _joinedLabel    = null!;
    private Label    _statusLabel    = null!;
    private CancellationTokenSource? _cts;

    public BlueskyProfileForm()
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
        ClearProfile();

        try
        {
            using var client = new BlueskyAnalyticsClient();
            await client.AuthenticateAsync(authCred.Value.Handle, authCred.Value.Password, _cts.Token);
            SetStatus($"Fetching profile for @{handle}…");
            var p = await client.GetProfileAsync(handle, _cts.Token);
            ShowProfile(p);
            SetStatus($"Profile loaded for @{p.Handle}.");
        }
        catch (OperationCanceledException) { SetStatus("Cancelled."); }
        catch (Exception ex)              { SetStatus($"Error: {ex.Message}"); }
        finally                            { SetBusy(false); }
    }

    private void ShowProfile(BskyProfile p)
    {
        _displayName.Text    = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Handle : p.DisplayName;
        _handleLabel.Text    = $"@{p.Handle}";
        _bioBox.Text         = p.Description;
        _followersValue.Text = p.FollowersCount.ToString("N0");
        _followingValue.Text = p.FollowsCount.ToString("N0");
        _postsValue.Text     = p.PostsCount.ToString("N0");

        if (DateTime.TryParse(p.CreatedAt, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var joined))
            _joinedLabel.Text = $"Member since {joined.ToLocalTime():MMMM d, yyyy}";
        else
            _joinedLabel.Text = "";
    }

    private void ClearProfile()
    {
        _displayName.Text    = "—";
        _handleLabel.Text    = "";
        _bioBox.Text         = "";
        _followersValue.Text = "—";
        _followingValue.Text = "—";
        _postsValue.Text     = "—";
        _joinedLabel.Text    = "";
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
