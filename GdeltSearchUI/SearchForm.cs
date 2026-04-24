using System.Diagnostics;
using System.Net.Http.Json;

namespace GdeltSearchUI;

public sealed class SearchForm : Form
{
    // ── Controls ────────────────────────────────────────────────────────────
    private readonly TextBox _queryBox;
    private readonly ComboBox _timespanBox;
    private readonly ComboBox _modeBox;
    private readonly Button _searchButton;
    private readonly DataGridView _grid;
    private readonly StatusStrip _status;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripProgressBar _progress;

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private CancellationTokenSource? _cts;
    private Font? _underlineFont;

    private static readonly (string Label, string Value)[] Timespans =
    [
        ("15 minutes", "15min"),
        ("30 minutes", "30min"),
        ("1 hour",     "1h"),
        ("3 hours",    "3h"),
        ("6 hours",    "6h"),
        ("12 hours",   "12h"),
        ("24 hours",   "24h"),
    ];

    private static readonly (string Label, string Value)[] Modes =
    [
        ("Article List",   "artlist"),
        ("Article + Geo",  "artgeo"),
    ];

    public SearchForm()
    {
        Text = "GDELT Article Search";
        Size = new Size(1100, 680);
        MinimumSize = new Size(800, 500);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        // ── Top toolbar panel ────────────────────────────────────────────
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8, 8, 8, 4),
            ColumnCount = 5,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));   // label
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // query
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));  // timespan
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));  // mode
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));   // button

        toolbar.Controls.Add(new Label
        {
            Text = "Query:",
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
        }, 0, 0);

        _queryBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "e.g.  military conflict Ukraine",
        };
        _queryBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = SearchAsync(); } };
        toolbar.Controls.Add(_queryBox, 1, 0);

        _timespanBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(4, 0, 4, 0),
        };
        foreach (var (label, _) in Timespans) _timespanBox.Items.Add(label);
        _timespanBox.SelectedIndex = 0;
        toolbar.Controls.Add(_timespanBox, 2, 0);

        _modeBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 4, 0),
        };
        foreach (var (label, _) in Modes) _modeBox.Items.Add(label);
        _modeBox.SelectedIndex = 0;
        toolbar.Controls.Add(_modeBox, 3, 0);

        _searchButton = new Button
        {
            Text = "Search",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _searchButton.FlatAppearance.BorderSize = 0;
        _searchButton.Click += async (_, _) => await SearchAsync();
        toolbar.Controls.Add(_searchButton, 4, 0);

        // ── Results grid ─────────────────────────────────────────────────
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
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            Cursor = Cursors.Default,
        };
        _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grid.DefaultCellStyle.Padding = new Padding(4, 3, 4, 3);
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _grid.EnableHeadersVisualStyles = false;
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);

        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Title",
            HeaderText = "Title",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 55,
            DefaultCellStyle = { ForeColor = Color.FromArgb(0, 70, 180) },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Domain",
            HeaderText = "Domain",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 16,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Tone",
            HeaderText = "Tone",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 7,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight },
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Language",
            HeaderText = "Language",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 10,
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Date",
            HeaderText = "Date",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 12,
        });

        _grid.CellFormatting += Grid_CellFormatting;
        _grid.CellMouseEnter += (_, e) =>
        {
            if (e.RowIndex >= 0) _grid.Cursor = Cursors.Hand;
        };
        _grid.CellMouseLeave += (_, _) => _grid.Cursor = Cursors.Default;
        _grid.CellClick += Grid_CellClick;

        // ── Status bar ───────────────────────────────────────────────────
        _statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _progress = new ToolStripProgressBar { Visible = false, Width = 120 };
        _status = new StatusStrip();
        _status.Items.Add(_statusLabel);
        _status.Items.Add(_progress);

        Controls.Add(_grid);
        Controls.Add(toolbar);
        Controls.Add(_status);

        SetStatus("Enter a query and press Search.");
    }

    // ── Search ───────────────────────────────────────────────────────────────

    private async Task SearchAsync()
    {
        var query = _queryBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            SetStatus("Please enter a search query.");
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SetBusy(true);
        _grid.Rows.Clear();

        var timespan = Timespans[_timespanBox.SelectedIndex].Value;
        var mode = Modes[_modeBox.SelectedIndex].Value;
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"https://api.gdeltproject.org/api/v2/doc/doc?query={encodedQuery}&mode={mode}&format=json&timespan={timespan}";

        try
        {
            var response = await _http.GetFromJsonAsync<GdeltResponse>(url, _cts.Token);
            var articles = response?.Articles ?? [];

            if (articles.Count == 0)
            {
                SetStatus("No articles found for that query and timespan.");
                return;
            }

            foreach (var a in articles)
            {
                var toneStr = a.Tone != 0 ? a.Tone.ToString("+0.00;-0.00") : "0.00";
                var dateStr = a.ParsedDate.HasValue
                    ? a.ParsedDate.Value.ToString("MMM d, HH:mm")
                    : a.SeenDate;

                var row = _grid.Rows[_grid.Rows.Add(a.Title, a.Domain, toneStr, a.Language, dateStr)];
                row.Tag = a.Url;

                // Tone colouring
                if (a.Tone < -3) row.Cells["Tone"].Style.ForeColor = Color.Firebrick;
                else if (a.Tone > 3) row.Cells["Tone"].Style.ForeColor = Color.SeaGreen;
            }

            SetStatus($"{articles.Count} article{(articles.Count == 1 ? "" : "s")} found — click a row to open in browser.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Search cancelled.");
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ── Grid events ──────────────────────────────────────────────────────────

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != _grid.Columns["Title"]!.Index) return;
        _underlineFont ??= new Font(_grid.Font, FontStyle.Underline);
        _grid.Rows[e.RowIndex].Cells["Title"].Style.Font = _underlineFont;
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var url = _grid.Rows[e.RowIndex].Tag as string;
        if (!string.IsNullOrEmpty(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetBusy(bool busy)
    {
        _searchButton.Enabled = !busy;
        _searchButton.Text = busy ? "Searching…" : "Search";
        _progress.Visible = busy;
        if (busy) _progress.Style = ProgressBarStyle.Marquee;
    }

    private void SetStatus(string message) => _statusLabel.Text = message;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _underlineFont?.Dispose();
            _cts?.Dispose();
            _http.Dispose();
        }
        base.Dispose(disposing);
    }
}
