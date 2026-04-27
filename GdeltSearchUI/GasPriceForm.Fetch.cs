namespace GdeltSearchUI;

internal partial class GasPriceForm
{
    private async Task FetchAsync()
    {
        var apiKey = CredentialManager.LoadEiaApiKey();
        if (apiKey is null)
        {
            apiKey = PromptForApiKey();
            if (apiKey is null) { SetStatus("No API key — fetch cancelled."); return; }
            CredentialManager.SaveEiaApiKey(apiKey);
        }

        SetBusy(true);
        ClearPrices();

        NationalGasPrices result;
        using (var client = new GasPriceApiClient(apiKey))
        {
            try   { result = await client.GetNationalAveragesAsync(); }
            catch (Exception ex) { ShowError(ex.Message); SetBusy(false); return; }
        }

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage!);
            SetBusy(false);
            return;
        }

        _lastResult = result;
        _regularLabel.Text  = Fmt(result.Regular);
        _midGradeLabel.Text = Fmt(result.MidGrade);
        _premiumLabel.Text  = Fmt(result.Premium);
        _dieselLabel.Text   = Fmt(result.Diesel);
        _postButton.Enabled = true;

        SetStatus($"Week of {result.Period}  ·  source: EIA");
        SetBusy(false);
    }

    private string? PromptForApiKey()
    {
        using var dlg = new Form
        {
            Text = "EIA API Key",
            Size = new Size(400, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = DarkTheme.Background,
            Font = new Font("Segoe UI", 9.5f),
        };

        var lbl = new Label
        {
            Text = "Enter your free EIA API key (eia.gov/opendata):",
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

        return dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(box.Text)
            ? box.Text.Trim()
            : null;
    }
}
