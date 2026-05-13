namespace GdeltSearchUI;

internal sealed partial class BlueskyProfileForm
{
    private void BuildLayout()
    {
        Text            = "Account Profile";
        Size            = new Size(460, 420);
        MinimumSize     = new Size(460, 420);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = DarkTheme.Background;

        _statusLabel = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(8, 0, 0, 0),
            ForeColor = DarkTheme.TextMuted,
            BackColor = DarkTheme.Surface,
            Font      = new Font("Segoe UI", 8.5f),
            Text      = "Enter a handle and click Fetch.",
        };

        Controls.Add(BuildContent());
        Controls.Add(BuildToolbar());
        Controls.Add(_statusLabel);
    }

    private Panel BuildToolbar()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = DarkTheme.Surface };

        var lbl = new Label
        {
            Text      = "Account:",
            AutoSize  = true,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Top = 12, Left = 10,
            Font = new Font("Segoe UI", 9f),
        };

        _handleBox = new TextBox
        {
            Top = 9, Left = 76, Width = 230, Height = 24,
            BackColor       = DarkTheme.Input,
            ForeColor       = DarkTheme.TextPrimary,
            BorderStyle     = BorderStyle.FixedSingle,
            Font            = new Font("Segoe UI", 9f),
            PlaceholderText = "handle.bsky.social",
        };
        _handleBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FetchAsync(); }
        };

        _fetchBtn = new Button
        {
            Text = "Fetch", Top = 8, Left = 314, Width = 72, Height = 26,
            BackColor = DarkTheme.AccentBlue, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        _fetchBtn.FlatAppearance.BorderSize = 0;
        _fetchBtn.Click += async (_, _) => await FetchAsync();

        panel.Controls.AddRange([lbl, _handleBox, _fetchBtn]);
        return panel;
    }

    private TableLayoutPanel BuildContent()
    {
        var tbl = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 7,
            Padding     = new Padding(16, 12, 16, 8),
            BackColor   = DarkTheme.Background,
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));  // display name
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));  // @handle
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));   // separator
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // bio
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));   // separator
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));  // stats
        tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // joined + fill

        // Row 0: display name
        _displayName = new Label
        {
            Text      = "—",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
        };
        tbl.Controls.Add(_displayName, 0, 0);

        // Row 1: @handle
        _handleLabel = new Label
        {
            Text      = "",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9.5f),
        };
        tbl.Controls.Add(_handleLabel, 0, 1);

        // Row 2: separator
        tbl.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Raised }, 0, 2);

        // Row 3: bio
        _bioBox = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BackColor   = DarkTheme.Background,
            ForeColor   = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.None,
            Font        = new Font("Segoe UI", 9.5f),
            ScrollBars  = RichTextBoxScrollBars.Vertical,
            Margin      = new Padding(0, 6, 0, 6),
        };
        tbl.Controls.Add(_bioBox, 0, 3);

        // Row 4: separator
        tbl.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Raised }, 0, 4);

        // Row 5: stats (3 columns)
        tbl.Controls.Add(BuildStats(), 0, 5);

        // Row 6: joined date
        _joinedLabel = new Label
        {
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 9f),
            Padding   = new Padding(0, 8, 0, 0),
        };
        tbl.Controls.Add(_joinedLabel, 0, 6);

        return tbl;
    }

    private TableLayoutPanel BuildStats()
    {
        var tbl = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 2,
            BackColor   = Color.Transparent,
            Margin      = new Padding(0, 8, 0, 0),
        };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        (string Name, int Col)[] cols =
        [
            ("Followers", 0),
            ("Following", 1),
            ("Posts",     2),
        ];

        foreach (var (name, col) in cols)
        {
            tbl.Controls.Add(new Label
            {
                Text      = name,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomCenter,
                ForeColor = DarkTheme.TextMuted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8.5f),
            }, col, 0);
        }

        _followersValue = MakeBigStat("—");
        _followingValue = MakeBigStat("—");
        _postsValue     = MakeBigStat("—");
        tbl.Controls.Add(_followersValue, 0, 1);
        tbl.Controls.Add(_followingValue, 1, 1);
        tbl.Controls.Add(_postsValue,     2, 1);

        return tbl;
    }

    private static Label MakeBigStat(string text) => new()
    {
        Text      = text,
        Dock      = DockStyle.Fill,
        TextAlign = ContentAlignment.TopCenter,
        ForeColor = DarkTheme.TextPrimary,
        BackColor = Color.Transparent,
        Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
    };
}
