namespace GdeltSearchUI;

internal static class ErrorDialog
{
    public static void Show(IWin32Window owner, string message)
    {
        var dlg = new Form
        {
            Text = "Error",
            Size = new Size(520, 240),
            MinimumSize = new Size(360, 180),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.Sizable,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = DarkTheme.Background,
        };

        var txt = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = message,
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 9f),
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        ok.FlatAppearance.BorderColor = DarkTheme.Input;

        dlg.Controls.Add(txt);
        dlg.Controls.Add(ok);
        dlg.AcceptButton = ok;
        dlg.Shown += (_, _) => { txt.SelectAll(); txt.Focus(); };
        dlg.ShowDialog(owner);
    }
}
