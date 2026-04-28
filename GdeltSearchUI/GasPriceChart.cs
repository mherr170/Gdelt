using ScottPlot;

namespace GdeltSearchUI;

internal static class GasPriceChart
{
    /// <summary>
    /// Renders a multi-line chart of monthly average pump prices for each
    /// fuel type and returns it as PNG bytes for embedding in a Bluesky post.
    /// </summary>
    public static byte[] RenderMonthlyAveragesPng(
        IReadOnlyList<NationalGasPrices> weekly, int monthLimit = 3, int width = 900, int height = 420)
    {
        var monthly = ComputeMonthlyAverages(weekly);
        if (monthLimit > 0 && monthly.Count > monthLimit)
            monthly = monthly.TakeLast(monthLimit).ToList();
        if (monthly.Count == 0) return [];

        var plot = new Plot();

        var xs = monthly.Select(m => m.Month.ToOADate()).ToArray();

        AddSeries(plot, xs, monthly.Select(m => m.Regular).ToArray(),  "Regular",   "#4FB56E");
        AddSeries(plot, xs, monthly.Select(m => m.MidGrade).ToArray(), "Mid-Grade", "#1D83BD");
        AddSeries(plot, xs, monthly.Select(m => m.Premium).ToArray(),  "Premium",   "#E5B14B");
        AddSeries(plot, xs, monthly.Select(m => m.Diesel).ToArray(),   "Diesel",    "#E5614B");

        plot.Axes.DateTimeTicksBottom();
        plot.YLabel("Price ($/gal)");
        plot.Title($"US Gas Prices — Monthly Averages (Last {monthly.Count} Months)");
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

    private static void AddSeries(Plot plot, double[] xs, double?[] ys, string label, string hex)
    {
        // Drop months where this fuel had no data
        var pairs = xs.Zip(ys, (x, y) => (x, y)).Where(p => p.y.HasValue).ToArray();
        if (pairs.Length == 0) return;

        var line = plot.Add.Scatter(
            pairs.Select(p => p.x).ToArray(),
            pairs.Select(p => p.y!.Value).ToArray());
        line.LegendText = label;
        line.LineWidth  = 2.5f;
        line.MarkerStyle.Size = 5;
        line.Color = ScottPlot.Color.FromHex(hex);
    }

    public static List<MonthlyAverage> ComputeMonthlyAverages(IReadOnlyList<NationalGasPrices> weekly)
    {
        return weekly
            .Select(w => (parsed: DateTime.TryParse(w.Period, out var d) ? d : (DateTime?)null, row: w))
            .Where(x => x.parsed.HasValue)
            .GroupBy(x => new DateTime(x.parsed!.Value.Year, x.parsed.Value.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyAverage
            {
                Month    = g.Key,
                Regular  = Avg(g, r => r.Regular),
                MidGrade = Avg(g, r => r.MidGrade),
                Premium  = Avg(g, r => r.Premium),
                Diesel   = Avg(g, r => r.Diesel),
            })
            .ToList();

        static double? Avg(IEnumerable<(DateTime? parsed, NationalGasPrices row)> rows, Func<NationalGasPrices, double?> sel)
        {
            var values = rows.Select(r => sel(r.row)).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
            return values.Length == 0 ? null : values.Average();
        }
    }
}

internal sealed record MonthlyAverage
{
    public DateTime Month    { get; init; }
    public double?  Regular  { get; init; }
    public double?  MidGrade { get; init; }
    public double?  Premium  { get; init; }
    public double?  Diesel   { get; init; }
}
