namespace GdeltSearchUI;

internal sealed partial class BlueskyBestHourForm
{
    private void BuildLayout()
    {
        Text            = "Best Hour to Post";
        Size            = new Size(680, 580);
        MinimumSize     = new Size(560, 420);
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
        var panel = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = DarkTheme.Surface };

        panel.Controls.Add(new Label
        {
            Text = "Account:", AutoSize = true, Top = 12, Left = 10,
            ForeColor = DarkTheme.TextMuted, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
        });

        _handleBox = new TextBox
        {
            Top = 9, Left = 76, Width = 240, Height = 24,
            BackColor = DarkTheme.Input, ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f),
            PlaceholderText = "handle.bsky.social",
        };
        _handleBox.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await FetchAsync(); }
        };

        _fetchBtn = new Button
        {
            Text = "Fetch", Top = 8, Left = 324, Width = 72, Height = 26,
            BackColor = DarkTheme.AccentBlue, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        _fetchBtn.FlatAppearance.BorderSize = 0;
        _fetchBtn.Click += async (_, _) => await FetchAsync();

        panel.Controls.AddRange([_handleBox, _fetchBtn]);
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

        var right = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        var bar   = new DataGridViewCellStyle
        {
            Font      = new Font("Consolas", 9f),
            ForeColor = Color.FromArgb(0x4F, 0xB5, 0x6E),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hour",       HeaderText = "Hour",        Width = 110 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Posts",      HeaderText = "Posts",       Width = 56,  DefaultCellStyle = right });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgLikes",   HeaderText = "Avg ❤",       Width = 72,  DefaultCellStyle = right });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgReposts", HeaderText = "Avg 🔁",      Width = 72,  DefaultCellStyle = right });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgEngage",  HeaderText = "Avg Engage",  Width = 88,  DefaultCellStyle = right });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bar",        HeaderText = "▓ Relative",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120, DefaultCellStyle = bar });

        _grid.CellFormatting += OnCellFormatting;
        return _grid;
    }
}
