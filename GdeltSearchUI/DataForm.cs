namespace GdeltSearchUI;

internal abstract class DataForm : Form
{
    protected abstract void SetStatus(string msg);

    protected void ShowError(string message)
    {
        SetStatus("Error — see details.");
        ErrorDialog.Show(this, message);
    }
}
