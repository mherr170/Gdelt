namespace GdeltSearchUI;

internal partial class ApodForm
{
    private Panel BuildToolbar()
    {
        var bar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = DarkTheme.Surface,
            Padding   = new Padding(8, 8, 8, 8),
        };

        var apiKeyBtn = new Button
        {
            Text      = "⚙ API Key",
            Dock      = DockStyle.Right,
            Width     = 84,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        apiKeyBtn.FlatAppearance.BorderSize = 0;
        apiKeyBtn.Click += (_, _) =>
        {
            var key = PromptForApiKey("NASA API key (free at api.nasa.gov):");
            if (key is not null) CredentialManager.SaveNasaApiKey(key);
        };

        var accountBtn = new Button
        {
            Text      = "⚙ Account",
            Dock      = DockStyle.Right,
            Width     = 84,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        accountBtn.FlatAppearance.BorderSize = 0;
        accountBtn.Click += (_, _) =>
        {
            using var dlg = new SettingsDialog(
                CredentialManager.LoadApodBluesky,
                CredentialManager.SaveApodBluesky,
                "Bluesky Account — NASA APOD");
            dlg.ShowDialog(this);
        };

        _postBtn = new Button
        {
            Text      = "Post",
            Dock      = DockStyle.Right,
            Width     = 150,
            BackColor = DarkTheme.PostButtonDefault,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Enabled   = false,
        };
        _postBtn.FlatAppearance.BorderSize = 0;
        _postBtn.Click += async (_, _) => await PostToBlueskyAsync();

        _refreshBtn = new Button
        {
            Text      = "Refresh",
            Dock      = DockStyle.Right,
            Width     = 70,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 4, 0),
        };
        _refreshBtn.FlatAppearance.BorderSize = 0;
        _refreshBtn.Click += async (_, _) => await FetchAsync();

        var heading = new Label
        {
            Text      = "NASA — Astronomy Picture of the Day",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f),
        };

        bar.Controls.Add(apiKeyBtn);
        bar.Controls.Add(accountBtn);
        bar.Controls.Add(_postBtn);
        bar.Controls.Add(_refreshBtn);
        bar.Controls.Add(heading);
        return bar;
    }

    private SplitContainer BuildMainPanel()
    {
        var split = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            Orientation   = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor     = DarkTheme.Background,
        };


        // Left: image
        _pictureBox = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
        };
        split.Panel1.Controls.Add(_pictureBox);

        // Right: metadata + explanation
        var meta = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            ColumnCount = 1,
            RowCount    = 4,
            AutoSize    = true,
            Padding     = new Padding(12, 10, 12, 4),
            BackColor   = DarkTheme.Background,
        };
        meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        meta.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _titleLabel = new Label
        {
            Text      = "—",
            Dock      = DockStyle.Fill,
            AutoSize  = false,
            Height    = 48,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextPrimary,
            Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
        };

        _dateLabel = new Label
        {
            Text      = "",
            Dock      = DockStyle.Fill,
            AutoSize  = false,
            Height    = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            Font      = new Font("Segoe UI", 9f),
        };

        _creditLabel = new Label
        {
            Text      = "",
            Dock      = DockStyle.Fill,
            AutoSize  = false,
            Height    = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Italic),
        };

        _typeLabel = new Label
        {
            Text      = "",
            Dock      = DockStyle.Fill,
            AutoSize  = false,
            Height    = 20,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(0xB8, 0x76, 0x0B),
            Font      = new Font("Segoe UI", 8.5f),
        };

        meta.Controls.Add(_titleLabel,  0, 0);
        meta.Controls.Add(_dateLabel,   0, 1);
        meta.Controls.Add(_creditLabel, 0, 2);
        meta.Controls.Add(_typeLabel,   0, 3);

        _explanationBox = new RichTextBox
        {
            Dock      = DockStyle.Fill,
            ReadOnly  = true,
            BackColor = DarkTheme.Background,
            ForeColor = DarkTheme.TextMuted,
            BorderStyle = BorderStyle.None,
            Font      = new Font("Segoe UI", 9f),
            Padding   = new Padding(12, 4, 12, 8),
            ScrollBars = RichTextBoxScrollBars.Vertical,
        };

        split.Panel2.Controls.Add(_explanationBox);
        split.Panel2.Controls.Add(meta);
        split.Panel2.BackColor = DarkTheme.Background;

        return split;
    }

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
