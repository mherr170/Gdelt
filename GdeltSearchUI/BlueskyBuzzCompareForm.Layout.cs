namespace GdeltSearchUI;

internal sealed partial class BlueskyBuzzCompareForm
{
    private void BuildLayout()
    {
        Text            = "Buzz Compare";
        Size            = new Size(700, 480);
        MinimumSize     = new Size(600, 400);
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
            Text      = "Enter up to 4 keywords and click Compare.",
        };

        Controls.Add(BuildGrid());
        Controls.Add(BuildKeywordPanel());
        Controls.Add(_statusLabel);
    }

    private Panel BuildKeywordPanel()
    {
        var panel = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 130,
            BackColor = DarkTheme.Surface,
            Padding   = new Padding(10, 8, 10, 8),
        };

        _keywordBoxes = new TextBox[4];
        string[] labels = ["Keyword 1:", "Keyword 2:", "Keyword 3:", "Keyword 4:"];

        for (int i = 0; i < 4; i++)
        {
            int top = 10 + i * 28;

            panel.Controls.Add(new Label
            {
                Text = labels[i], AutoSize = true, Top = top + 4, Left = 10,
                Width = 76, ForeColor = DarkTheme.TextMuted,
                BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f),
            });

            _keywordBoxes[i] = new TextBox
            {
                Top = top, Left = 90, Width = 440, Height = 22,
                BackColor = DarkTheme.Input, ForeColor = DarkTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9f),
                PlaceholderText = i == 0 ? "e.g. climate change" : "",
            };
            panel.Controls.Add(_keywordBoxes[i]);
        }

        _compareBtn = new Button
        {
            Text = "Compare", Top = 10, Left = 540, Width = 100, Height = 106,
            BackColor = DarkTheme.AccentBlue, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
        };
        _compareBtn.FlatAppearance.BorderSize = 0;
        _compareBtn.Click += async (_, _) => await CompareAsync();
        panel.Controls.Add(_compareBtn);

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

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Keyword",      HeaderText = "Keyword",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 120, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Posts",        HeaderText = "Posts",        Width = 62,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalLikes",   HeaderText = "❤ Total Likes", Width = 90,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalReposts", HeaderText = "🔁 Reposts",    Width = 78,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgLikes",     HeaderText = "Avg ❤",        Width = 62,  DefaultCellStyle = right, SortMode = DataGridViewColumnSortMode.Automatic });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bar",          HeaderText = "▓ Buzz",       Width = 140, DefaultCellStyle = bar });

        return _grid;
    }
}
