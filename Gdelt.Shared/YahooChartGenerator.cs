using ScottPlot;
using ScottColor = ScottPlot.Color;

namespace GdeltSearchUI;

internal static class YahooChartGenerator
{
    private static readonly (string Code, string Label, string Hex)[] Series =
    [
        ("BRENT_CRUDE",   "Brent",    "#4A9FE0"),
        ("WTI_CRUDE",     "WTI",      "#4FB56E"),
        ("NATURAL_GAS",   "Nat Gas",  "#F0A832"),
        ("RBOB_GASOLINE", "RBOB",     "#E06060"),
        ("HEATING_OIL",   "Htg Oil",  "#B07AE0"),
    ];

    public static byte[] RenderPng(IReadOnlyList<CommodityHistoryPoint> history,
        int width = 900, int height = 400)
    {
        if (history.Count == 0) return [];

        var plot    = new Plot();
        var plotted = 0;

        var ordered = history.OrderBy(h => h.Timestamp).ToList();

        foreach (var (code, label, hex) in Series)
        {
            var points = ordered.Where(h => h.Prices.ContainsKey(code)).ToList();
            if (points.Count == 0) continue;

            var baseline = points[0].Prices[code];
            if (baseline == 0) continue;

            var xs = points.Select(p => p.Timestamp.UtcDateTime.ToOADate()).ToArray();
            var ys = points.Select(p => (p.Prices[code] - baseline) / baseline * 100.0).ToArray();

            var line = plot.Add.Scatter(xs, ys);
            line.LegendText       = label;
            line.Color            = ScottColor.FromHex(hex);
            line.LineWidth        = 2.5f;
            line.MarkerStyle.Size = points.Count == 1 ? 8 : 5;
            plotted++;
        }

        if (plotted == 0) return [];

        var dateTicks = new ScottPlot.TickGenerators.DateTimeAutomatic();
        dateTicks.LabelFormatter = dt => dt.ToString("dd MMM\nHH:mm");
        plot.Axes.Bottom.TickGenerator = dateTicks;
        plot.XLabel("UTC");
        plot.YLabel("% change from first post");
        plot.Title($"Yahoo Futures — Post History ({ordered.Count} snapshot{(ordered.Count != 1 ? "s" : "")})");
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
