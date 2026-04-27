namespace GdeltSearchUI;

internal sealed class LauncherForm : Form
{
    private static readonly (string Label, string Query)[] SearchPresets =
    [
        ("USGunV", "shooting"),
    ];

    public LauncherForm()
    {
        Text = "GDELT Launcher";
        Size = new Size(360, 200);
        MinimumSize = new Size(280, 160);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = DarkTheme.Background;

        Controls.Add(BuildButtonGrid());
        Controls.Add(BuildHeader());
    }

    private static Label BuildHeader()
    {
        return new Label
        {
            Text = "Select a search preset",
            Dock = DockStyle.Top,
            Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = DarkTheme.TextPrimary,
            BackColor = DarkTheme.Surface,
        };
    }

    private FlowLayoutPanel BuildButtonGrid()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 16),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = DarkTheme.Background,
        };

        foreach (var (label, query) in SearchPresets)
            panel.Controls.Add(MakeSearchButton(label, query));

        panel.Controls.Add(MakeGasPriceButton());
        panel.Controls.Add(MakeQuakeButton());

        return panel;
    }

    private Button MakeSearchButton(string label, string query)
    {
        var btn = MakeButton(label, DarkTheme.PresetBlue);
        btn.Click += (_, _) => new SearchForm(initialQuery: query).Show();
        return btn;
    }

    private Button MakeGasPriceButton()
    {
        var btn = MakeButton("US Gas $", Color.FromArgb(0x4A, 0x7C, 0x3F));
        btn.Click += (_, _) => new GasPriceForm().Show();
        return btn;
    }

    private Button MakeQuakeButton()
    {
        var btn = MakeButton("Quake", Color.FromArgb(0x8B, 0x45, 0x13));
        btn.Click += (_, _) => new QuakeForm().Show();
        return btn;
    }

    private static Button MakeButton(string label, Color backColor)
    {
        var btn = new Button
        {
            Text = label,
            AutoSize = false,
            Width = 90,
            Height = 34,
            Margin = new Padding(0, 0, 8, 8),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }
}
