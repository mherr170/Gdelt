namespace GdeltSearchUI;

internal sealed partial class BlueskyTopicSearchForm
{
    private void BuildLayout()
    {
        Text            = "Topic Search";
        Size            = new Size(820, 560);
        MinimumSize     = new Size(640, 400);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = DarkTheme.Background;

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom, Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(8, 0, 0, 0),
            ForeColor = DarkTheme.TextMuted,
            BackColor = DarkTheme.Surface,
            Font      = new Font("Segoe UI", 8.5f),
            Text      = "Enter a query and click Search.",
        };

        Controls.Add(BuildGrid());
        Controls.Add(BuildToolbar());
        Controls.Add(_statusLabel);
    }

    private Panel BuildToolbar()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = DarkTheme.Surface };

        panel.Controls.Add(new Label
        {
            Text = "Query:", AutoSize = true, Top = 12, Left = 10,
            ForeColor = DarkTheme.TextMuted, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
        });

        _queryBox = new TextBox
        {
            Top = 9, Left = 64, Width = 380, Height = 24,
            BackColor = DarkTheme.Input, ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f),
            PlaceholderText = "keyword, phrase, or #hashtag",
        };
        _queryBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FetchAsync(); }
        };

        _fetchBtn = new Button
        {
            Text = "Search", Top = 8, Left = 452, Width = 80, Height = 26,
            BackColor = DarkTheme.AccentBlue, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        _fetchBtn.FlatAppearance.BorderSize = 0;
        _fetchBtn.Click += async (_, _) => await FetchAsync();

        panel.Controls.AddRange([_queryBox, _fetchBtn]);
        return panel;
    }

    private DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = DarkTheme.Background, GridColor = DarkTheme.Raised,
            BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9f),
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 26, EnableHeadersVisualStyles = false,
        };
        ApplyGridStyles(_grid);

        var right = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",    HeaderText = "Date",      Width = 84, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Author",  HeaderText = "Author",    Width = 130, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Post",    HeaderText = "Post",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Likes",   HeaderText = "❤ Likes",   Width = 65,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Reposts", HeaderText = "🔁 Reposts", Width = 74,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Replies", HeaderText = "💬 Replies", Width = 74,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });

        _grid.CellDoubleClick += OnRowDoubleClick;
        return _grid;
    }

    private static void ApplyGridStyles(DataGridView g)
    {
        g.DefaultCellStyle.BackColor          = DarkTheme.Background;
        g.DefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        g.DefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        g.DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        g.DefaultCellStyle.Padding            = new Padding(4, 2, 4, 2);
        g.AlternatingRowsDefaultCellStyle.BackColor          = DarkTheme.Surface;
        g.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        g.ColumnHeadersDefaultCellStyle.BackColor = DarkTheme.Raised;
        g.ColumnHeadersDefaultCellStyle.ForeColor = DarkTheme.TextPrimary;
        g.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkTheme.Raised;
    }
}
