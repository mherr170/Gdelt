namespace GdeltSearchUI;

// Shared "enter an API key" dialog, usable from any owner window — not just
// DataForm subclasses (e.g. LauncherForm, which is a plain Form).
internal static class ApiKeyPrompt
{
    public static string? Show(IWin32Window owner, string promptText)
    {
        using var dlg = new Form
        {
            Text = "API Key",
            Size = new Size(420, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = DarkTheme.Background,
            Font = new Font("Segoe UI", 9.5f),
        };

        var lbl = new Label
        {
            Text = promptText,
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8, 10, 0, 0),
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
        };

        var box = new TextBox
        {
            Dock = DockStyle.Top,
            Height = 24,
            Margin = new Padding(8, 0, 8, 0),
            BackColor = DarkTheme.Input,
            ForeColor = DarkTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var ok = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        ok.FlatAppearance.BorderColor = DarkTheme.Input;

        dlg.Controls.Add(ok);
        dlg.Controls.Add(box);
        dlg.Controls.Add(lbl);
        dlg.AcceptButton = ok;

        return dlg.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(box.Text)
            ? box.Text.Trim()
            : null;
    }
}
