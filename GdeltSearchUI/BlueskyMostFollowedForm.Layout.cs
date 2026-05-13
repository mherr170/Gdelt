namespace GdeltSearchUI;

internal sealed partial class BlueskyMostFollowedForm
{
    private void BuildLayout()
    {
        Text            = "Most Followed Accounts";
        Size            = new Size(640, 520);
        MinimumSize     = new Size(520, 360);
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
            Text      = "Enter a topic to find the most-followed accounts.",
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
            Text = "Topic:", AutoSize = true, Top = 12, Left = 10,
            ForeColor = DarkTheme.TextMuted, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
        });

        _queryBox = new TextBox
        {
            Top = 9, Left = 58, Width = 280, Height = 24,
            BackColor = DarkTheme.Input, ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f),
            PlaceholderText = "e.g. climate, news, science",
        };
        _queryBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FetchAsync(); }
        };

        _fetchBtn = new Button
        {
            Text = "Search", Top = 8, Left = 346, Width = 80, Height = 26,
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

        var right  = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        var center = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rank",        HeaderText = "#",          Width = 38,  DefaultCellStyle = center });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Handle",      HeaderText = "Handle",     Width = 160, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "Display Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 160, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Followers",   HeaderText = "👥 Followers", Width = 92,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Posts",       HeaderText = "📝 Posts",     Width = 72,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });

        _grid.CellDoubleClick += OnRowDoubleClick;
        return _grid;
    }
}
