namespace GdeltSearchUI;

internal sealed record StockEntry(
    string                                        Symbol,
    string                                        DisplayName,
    double                                        Price,
    double                                        PreviousClose,
    double                                        ChangePercent,
    DateTimeOffset                                UpdatedAt,
    IReadOnlyList<(DateTime Time, double Price)>  Intraday);

internal static class StockIndex
{
    public static readonly (string Symbol, string DisplayName)[] Catalog =
    [
        ("^GSPC", "S&P 500"),
        ("^DJI",  "Dow Jones"),
        ("^IXIC", "Nasdaq"),
        ("^RUT",  "Russell 2K"),
    ];
}
