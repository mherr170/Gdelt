using ScottPlot;

namespace GdeltSearchUI;

internal static class DebtSparkline
{
    /// <summary>
    /// Renders a small line chart of total public debt over the supplied snapshots
    /// and returns it as PNG bytes suitable for embedding in a Bluesky post.
    /// </summary>
    public static byte[] RenderPng(IReadOnlyList<DebtSnapshot> snapshots, int width = 800, int height = 360)
    {
        var plot = new Plot();

        var xs = snapshots.Select(s => s.RecordDate.ToDateTime(TimeOnly.MinValue).ToOADate()).ToArray();
        var ys = snapshots.Select(s => (double)(s.TotalPublicDebt / 1_000_000_000_000m)).ToArray();

        var line = plot.Add.Scatter(xs, ys);
        // Per-point dots are useful for a handful of days but turn into noise once
        // the series spans months — drop them past ~60 points and keep just the line.
        line.MarkerStyle.Size = snapshots.Count > 60 ? 0 : 6;
        line.LineWidth = 2.5f;
        line.Color = ScottPlot.Color.FromHex("#4FB56E");

        plot.Axes.DateTimeTicksBottom();
        plot.YLabel("Total Public Debt ($T)");

        var first = snapshots[0].RecordDate;
        var last  = snapshots[^1].RecordDate;
        plot.Title($"US National Debt — {first:MMM d, yyyy} to {last:MMM d, yyyy}");

        // Dark theme to match the app
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");
        plot.DataBackground.Color   = ScottPlot.Color.FromHex("#2A2A2A");
        plot.Axes.Color(ScottPlot.Color.FromHex("#CCCCCC"));
        plot.Grid.MajorLineColor    = ScottPlot.Color.FromHex("#3A3A3A");

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
}
