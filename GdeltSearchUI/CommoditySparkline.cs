using ScottPlot;

namespace GdeltSearchUI;

internal static class CommoditySparkline
{
    // Subset shown on the chart — normalised % change makes mixed scales comparable.
    // Only daily series plotted — weekly heating oil/RBOB have too few points for a meaningful trend line.
    private static readonly (string Slug, string Label, string Hex)[] Series =
    [
        ("brent_crude_oil", "Brent",   "#4FB56E"),
        ("crude_oil",       "WTI",     "#4A9FE0"),
        ("natural_gas",     "Nat Gas", "#F0A832"),
    ];

    public static byte[] RenderPng(IReadOnlyList<CommodityHistoryPoint> history, int width = 900, int height = 400)
    {
        var plot    = new Plot();
        var plotted = 0;

        foreach (var (slug, label, hex) in Series)
        {
            var points = history
                .Where(h => h.Prices.ContainsKey(slug))
                .OrderBy(h => h.Timestamp)
                .ToList();
            if (points.Count < 2) continue;

            var baseline = points[0].Prices[slug];
            if (baseline == 0) continue;

            var xs = points.Select(p => p.Timestamp.UtcDateTime.ToOADate()).ToArray();
            var ys = points.Select(p => (p.Prices[slug] - baseline) / baseline * 100.0).ToArray();

            var line = plot.Add.Scatter(xs, ys);
            line.LegendText       = label;
            line.Color            = ScottPlot.Color.FromHex(hex);
            line.LineWidth        = 2.5f;
            line.MarkerStyle.Size = 5;
            plotted++;
        }

        if (plotted == 0) return [];

        plot.Axes.DateTimeTicksBottom();
        plot.YLabel("% Change from first snapshot");
        plot.Title($"Commodity Prices — % Change ({history.Count} snapshots)");
        plot.ShowLegend(Alignment.UpperLeft);

        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");
        plot.DataBackground.Color   = ScottPlot.Color.FromHex("#2A2A2A");
        plot.Axes.Color(ScottPlot.Color.FromHex("#CCCCCC"));
        plot.Grid.MajorLineColor    = ScottPlot.Color.FromHex("#3A3A3A");
        plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#2A2A2A");
        plot.Legend.FontColor       = ScottPlot.Color.FromHex("#CCCCCC");
        plot.Legend.OutlineColor    = ScottPlot.Color.FromHex("#3A3A3A");

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
}
