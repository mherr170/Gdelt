namespace GdeltSearchUI;

public sealed partial class SearchForm
{
    private TableLayoutPanel BuildToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8, 8, 8, 4),
            ColumnCount = 7,
            BackColor = DarkTheme.Surface,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

        toolbar.Controls.Add(new Label
        {
            Text = "Query:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
        }, 0, 0);

        _queryBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "e.g.  military conflict Ukraine",
            BackColor = DarkTheme.Input,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _queryBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = SearchAsync(); } };
        toolbar.Controls.Add(_queryBox, 1, 0);

        _timespanBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(4, 0, 4, 0),
            BackColor = DarkTheme.Input,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        foreach (var (label, _) in Timespans) _timespanBox.Items.Add(label);
        _timespanBox.SelectedIndex = 0;
        toolbar.Controls.Add(_timespanBox, 2, 0);

        _modeBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 4, 0),
            BackColor = DarkTheme.Input,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        foreach (var (label, _) in Modes) _modeBox.Items.Add(label);
        _modeBox.SelectedIndex = 0;
        toolbar.Controls.Add(_modeBox, 3, 0);

        _searchButton = new Button
        {
            Text = "Search",
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _searchButton.FlatAppearance.BorderSize = 0;
        _searchButton.Click += async (_, _) => await SearchAsync();
        toolbar.Controls.Add(_searchButton, 4, 0);

        _titleOnlyBox = new CheckBox
        {
            Text = "Title only",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(4, 0, 0, 0),
            Checked = true,
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
        };
        toolbar.Controls.Add(_titleOnlyBox, 5, 0);

        var accountBtn = new Button
        {
            Text = "⚙ Account",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 0),
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
        };
        accountBtn.FlatAppearance.BorderSize = 0;
        accountBtn.Click += (_, _) =>
        {
            using var dlg = _credLoader is not null && _credSaver is not null
                ? new SettingsDialog(_credLoader, _credSaver, _credTitle)
                : new SettingsDialog();
            dlg.ShowDialog(this);
        };
        toolbar.Controls.Add(accountBtn, 6, 0);

        return toolbar;
    }

    private DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = DarkTheme.Background,
            GridColor = DarkTheme.Raised,
            BorderStyle = BorderStyle.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Cursor = Cursors.Default,
            EnableHeadersVisualStyles = false,
        };

        _grid.DefaultCellStyle.BackColor         = DarkTheme.Background;
        _grid.DefaultCellStyle.ForeColor         = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        _grid.DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.WrapMode          = DataGridViewTriState.True;
        _grid.DefaultCellStyle.Padding           = new Padding(4, 3, 4, 3);

        _grid.AlternatingRowsDefaultCellStyle.BackColor          = DarkTheme.Surface;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;

        _grid.ColumnHeadersDefaultCellStyle.BackColor  = DarkTheme.Raised;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor  = DarkTheme.TextPrimary;
        _grid.ColumnHeadersDefaultCellStyle.Font       = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkTheme.Raised;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title",    HeaderText = "Title",    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55, DefaultCellStyle = { ForeColor = DarkTheme.TitleLink } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Domain",   HeaderText = "Domain",   AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tone",     HeaderText = "Tone",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 7,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Language", HeaderText = "Language", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",     HeaderText = "Date",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 12 });

        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellMouseEnter += (_, e) => { if (e.RowIndex >= 0) _grid.Cursor = Cursors.Hand; };
        _grid.CellMouseLeave += (_, _) => _grid.Cursor = Cursors.Default;
        _grid.CellClick      += Grid_CellClick;

        _grid.ContextMenuStrip = BuildGridContextMenu();

        return _grid;
    }

    private ContextMenuStrip BuildGridContextMenu()
    {
        var postItem = new ToolStripMenuItem("Post to Bluesky");
        postItem.Click += async (_, _) =>
        {
            if (_grid.CurrentRow is not { } row) return;
            var title = row.Cells["Title"].Value as string ?? "";
            var url   = row.Tag as string ?? "";
            await PostToBlueskyAsync(title, url);
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(postItem);
        menu.Opening += (_, e) =>
        {
            var hit = _grid.HitTest(
                _grid.PointToClient(Cursor.Position).X,
                _grid.PointToClient(Cursor.Position).Y);
            if (hit.RowIndex < 0) { e.Cancel = true; return; }
            _grid.CurrentCell = _grid.Rows[hit.RowIndex].Cells[0];
        };
        DarkTheme.ApplyToContextMenu(menu);
        return menu;
    }

    private StatusStrip BuildStatusBar()
    {
        _statusLabel = new ToolStripStatusLabel
        {
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
        };
        _progress = new ToolStripProgressBar { Visible = false, Width = 120 };

        var bar = new StatusStrip
        {
            BackColor = DarkTheme.Surface,
        };
        bar.Items.Add(_statusLabel);
        bar.Items.Add(_progress);
        return bar;
    }
}
