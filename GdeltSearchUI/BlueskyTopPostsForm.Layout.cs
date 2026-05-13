namespace GdeltSearchUI;

internal sealed partial class BlueskyTopPostsForm
{
    private void BuildLayout()
    {
        Text = _sortMode switch
        {
            BskySortMode.Likes   => "Top Posts — Likes",
            BskySortMode.Reposts => "Top Posts — Reposts",
            BskySortMode.Replies => "Top Posts — Replies",
            BskySortMode.Quotes  => "Top Posts — Quotes",
            _                    => "Top Posts",
        };

        Size            = new Size(820, 560);
        MinimumSize     = new Size(640, 400);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
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

        Controls.Add(BuildGrid());
        Controls.Add(BuildToolbar());
        Controls.Add(_statusLabel);
    }

    private Panel BuildToolbar()
    {
        var panel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 42,
            BackColor = DarkTheme.Surface,
        };

        var lbl = new Label
        {
            Text      = "Account:",
            AutoSize  = true,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Top       = 12,
            Left      = 10,
            Font      = new Font("Segoe UI", 9f),
        };

        _handleBox = new TextBox
        {
            Top             = 9,
            Left            = 76,
            Width           = 230,
            Height          = 24,
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
            Text      = "Fetch",
            Top       = 8,
            Left      = 314,
            Width     = 72,
            Height    = 26,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        _fetchBtn.FlatAppearance.BorderSize = 0;
        _fetchBtn.Click += async (_, _) => await FetchAsync();

        panel.Controls.AddRange([lbl, _handleBox, _fetchBtn]);
        return panel;
    }

    private DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock                  = DockStyle.Fill,
            ReadOnly              = true,
            AllowUserToAddRows    = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible     = false,
            SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect           = false,
            BackgroundColor       = DarkTheme.Background,
            GridColor             = DarkTheme.Raised,
            BorderStyle           = BorderStyle.None,
            Font                  = new Font("Segoe UI", 9f),
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight         = 26,
            EnableHeadersVisualStyles   = false,
        };

        _grid.DefaultCellStyle.BackColor          = DarkTheme.Background;
        _grid.DefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        _grid.DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.Padding            = new Padding(4, 2, 4, 2);

        _grid.AlternatingRowsDefaultCellStyle.BackColor          = DarkTheme.Surface;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;

        _grid.ColumnHeadersDefaultCellStyle.BackColor = DarkTheme.Raised;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkTheme.TextPrimary;
        _grid.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkTheme.Raised;

        var numStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Date", HeaderText = "Date", Width = 88,
            SortMode = DataGridViewColumnSortMode.Automatic,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Post", HeaderText = "Post",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            MinimumWidth = 200,
            SortMode = DataGridViewColumnSortMode.Automatic,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Likes", HeaderText = "❤ Likes", Width = 72,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = numStyle,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Reposts", HeaderText = "🔁 Reposts", Width = 80,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = numStyle,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Replies", HeaderText = "💬 Replies", Width = 80,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = numStyle,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Quotes", HeaderText = "💎 Quotes", Width = 72,
            SortMode = DataGridViewColumnSortMode.Automatic,
            DefaultCellStyle = numStyle,
        });

        // Gold-tint the column we sorted by.
        var primaryCol = _sortMode switch
        {
            BskySortMode.Likes   => "Likes",
            BskySortMode.Reposts => "Reposts",
            BskySortMode.Replies => "Replies",
            BskySortMode.Quotes  => "Quotes",
            _                    => "Likes",
        };
        _grid.Columns[primaryCol]!.DefaultCellStyle.ForeColor =
            Color.FromArgb(0xFF, 0xD7, 0x00);

        _grid.CellDoubleClick += OnRowDoubleClick;

        return _grid;
    }
}
