namespace GdeltSearchUI;

internal sealed class SettingsDialog : Form
{
    private readonly TextBox _handleBox;
    private readonly TextBox _passwordBox;
    private readonly Action<string, string> _save;

    // Default constructor — used by the news search window (original Bluesky account).
    public SettingsDialog()
        : this(CredentialManager.Load, CredentialManager.Save, "Bluesky Account — News") { }

    public SettingsDialog(
        Func<(string Handle, string Password)?> load,
        Action<string, string> save,
        string title = "Bluesky Account")
    {
        _save = save;

        Text = title;
        Size = new Size(420, 210);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = DarkTheme.Background;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 8),
            ColumnCount = 2,
            RowCount = 4,
            BackColor = DarkTheme.Background,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(MakeLabel("Handle:"), 0, 0);
        _handleBox = MakeInput("user.bsky.social");
        layout.Controls.Add(_handleBox, 1, 0);

        layout.Controls.Add(MakeLabel("App Password:"), 0, 1);
        _passwordBox = MakeInput("xxxx-xxxx-xxxx-xxxx", isPassword: true);
        layout.Controls.Add(_passwordBox, 1, 1);

        var hint = new Label
        {
            Text = "Generate an App Password at bsky.app › Settings › App Passwords",
            Dock = DockStyle.Fill,
            ForeColor = DarkTheme.TextMuted,
            Font = new Font("Segoe UI", 8.5f),
            BackColor = Color.Transparent,
        };
        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 2);

        var saveBtn   = MakeDialogButton("Save",   DialogResult.OK);
        var cancelBtn = MakeDialogButton("Cancel", DialogResult.Cancel);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
        };
        buttons.Controls.Add(cancelBtn);
        buttons.Controls.Add(saveBtn);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);
        AcceptButton = saveBtn;
        CancelButton = cancelBtn;

        var existing = load();
        if (existing.HasValue)
        {
            _handleBox.Text   = existing.Value.Handle;
            _passwordBox.Text = existing.Value.Password;
        }

        saveBtn.Click += OnSave;
    }

    private void OnSave(object? sender, EventArgs e)
    {
        var handle   = _handleBox.Text.Trim();
        var password = _passwordBox.Text.Trim();

        if (string.IsNullOrEmpty(handle) || string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Both fields are required.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        _save(handle, password);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        TextAlign = ContentAlignment.MiddleRight,
        Dock = DockStyle.Fill,
        ForeColor = DarkTheme.TextPrimary,
        BackColor = Color.Transparent,
    };

    private static TextBox MakeInput(string placeholder, bool isPassword = false) => new()
    {
        Dock = DockStyle.Fill,
        PlaceholderText = placeholder,
        UseSystemPasswordChar = isPassword,
        BackColor = DarkTheme.Input,
        ForeColor = DarkTheme.TextPrimary,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private static Button MakeDialogButton(string text, DialogResult result)
    {
        var btn = new Button
        {
            Text = text,
            DialogResult = result,
            AutoSize = true,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        btn.FlatAppearance.BorderColor = DarkTheme.Input;
        return btn;
    }
}
