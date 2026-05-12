using ScottPlot;
using ScottColor = ScottPlot.Color;

namespace GdeltSearchUI;

internal static class StockChartGenerator
{
    private static readonly (string Symbol, string Label, string Hex)[] Series =
    [
        ("^GSPC", "S&P 500",   "#4A9FE0"),
        ("^DJI",  "Dow",       "#4FB56E"),
        ("^IXIC", "Nasdaq",    "#F0A832"),
        ("^RUT",  "Russell 2K","#E06060"),
    ];

    public static byte[] RenderPng(IReadOnlyList<StockEntry> entries,
        int width = 900, int height = 400)
    {
        if (entries.Count == 0) return [];

        var plot    = new Plot();
        var plotted = 0;

        foreach (var (symbol, label, hex) in Series)
        {
            var entry = entries.FirstOrDefault(e => e.Symbol == symbol);
            if (entry is null || entry.Intraday.Count < 2) continue;

            var baseline = entry.PreviousClose > 0 ? entry.PreviousClose : entry.Intraday[0].Price;
            if (baseline == 0) continue;

            var xs = entry.Intraday.Select(p => p.Time.ToOADate()).ToArray();
            var ys = entry.Intraday.Select(p => (p.Price - baseline) / baseline * 100.0).ToArray();

            var line = plot.Add.Scatter(xs, ys);
            line.LegendText       = label;
            line.Color            = ScottColor.FromHex(hex);
            line.LineWidth        = 2.5f;
            line.MarkerStyle.Size = 0;
            plotted++;
        }

        if (plotted == 0) return [];

        // Zero reference line
        var zero = plot.Add.HorizontalLine(0);
        zero.Color     = ScottColor.FromHex("#555555");
        zero.LineWidth = 1f;

        var dateTicks = new ScottPlot.TickGenerators.DateTimeAutomatic();
        dateTicks.LabelFormatter = dt => dt.ToString("h:mm tt");
        plot.Axes.Bottom.TickGenerator = dateTicks;
        plot.XLabel("ET");
        plot.YLabel("% vs prior close");

        var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var etDate  = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, eastern).Date.ToString("MMM d, yyyy");
        plot.Title($"US Stock Indices — {etDate}");
        plot.ShowLegend(Alignment.UpperLeft);

        plot.FigureBackground.Color = ScottColor.FromHex("#1E1E1E");
        plot.DataBackground.Color   = ScottColor.FromHex("#2A2A2A");
        plot.Axes.Color(ScottColor.FromHex("#CCCCCC"));
        plot.Grid.MajorLineColor    = ScottColor.FromHex("#3A3A3A");
        plot.Legend.BackgroundColor = ScottColor.FromHex("#2A2A2A");
        plot.Legend.FontColor       = ScottColor.FromHex("#CCCCCC");
        plot.Legend.OutlineColor    = ScottColor.FromHex("#3A3A3A");

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
}
