namespace GdeltSearchUI;

internal abstract class DataForm : Form
{
    protected abstract void SetStatus(string msg);

    protected void ShowError(string message)
    {
        SetStatus("Error — see details.");
        ErrorDialog.Show(this, message);
    }

    // Factory for the status bar label — identical across all data forms.
    protected Label CreateStatusLabel() => new()
    {
        Dock      = DockStyle.Bottom,
        Height    = 24,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding   = new Padding(8, 0, 0, 0),
        ForeColor = DarkTheme.TextMuted,
        BackColor = DarkTheme.Surface,
        Font      = new Font("Segoe UI", 8.5f),
        Text      = "Loading…",
    };

    protected string? PromptForApiKey(string promptText) => ApiKeyPrompt.Show(this, promptText);
}
