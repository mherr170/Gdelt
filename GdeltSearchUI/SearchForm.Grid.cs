using System.Diagnostics;

namespace GdeltSearchUI;

public sealed partial class SearchForm
{
    private void PopulateGrid(List<GdeltArticle> articles)
    {
        _grid.Rows.Clear();
        foreach (var a in articles.OrderByDescending(a => a.ParsedDate ?? DateTime.MinValue))
        {
            var toneStr = a.Tone != 0 ? a.Tone.ToString("+0.00;-0.00") : "0.00";
            var dateStr = a.ParsedDate.HasValue ? a.ParsedDate.Value.ToString("MMM d, HH:mm") : a.SeenDate;
            var row = _grid.Rows[_grid.Rows.Add(a.Title, a.Domain, toneStr, a.Language, dateStr)];
            row.Tag = a.Url;
            if      (a.Tone < SearchConstants.ToneNegThreshold) row.Cells["Tone"].Style.ForeColor = DarkTheme.ToneNeg;
            else if (a.Tone > SearchConstants.TonePosThreshold) row.Cells["Tone"].Style.ForeColor = DarkTheme.TonePos;
        }
    }

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
}
