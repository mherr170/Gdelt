using System.Text.Json;

namespace GdeltSearchUI;

/// <summary>
/// Persists rolling daily commodity price snapshots (up to 30) for delta
/// calculation and sparklines.  One entry per calendar day; if multiple
/// fetches happen in a day the entry is replaced in-place.
/// </summary>
internal static class CommodityPriceCache
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "commodity_history.json");

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    private const int MaxPoints = 30;

    public static List<CommodityHistoryPoint> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            var text = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<CommodityHistoryPoint>>(text, _json) ?? [];
        }
        catch (Exception ex)
        {
            AppLogger.Log($"CommodityPriceCache load failed: {ex.Message}");
            return [];
        }
    }

    public static void Append(IEnumerable<CommodityPrice> prices)
    {
        var history  = Load();
        var now      = DateTimeOffset.Now;
        var today    = now.LocalDateTime.Date;
        var newPrices = prices.ToDictionary(p => p.Slug, p => p.Price);
        var point     = new CommodityHistoryPoint { Timestamp = now, Prices = newPrices };

        var todayIdx = history.FindIndex(h => h.Timestamp.LocalDateTime.Date == today);
        if (todayIdx >= 0)
            history[todayIdx] = point;
        else
            history.Add(point);

        if (history.Count > MaxPoints)
            history = history.TakeLast(MaxPoints).ToList();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(history, _json));
        }
        catch (Exception ex)
        {
            AppLogger.Log($"CommodityPriceCache save failed: {ex.Message}");
        }
    }

    /// <summary>Most-recent snapshot from a prior calendar day, for day-over-day delta.</summary>
    public static IReadOnlyDictionary<string, double>? GetPreviousPrices()
    {
        var history = Load();
        var today   = DateTimeOffset.Now.LocalDateTime.Date;
        var prev    = history.LastOrDefault(h => h.Timestamp.LocalDateTime.Date < today);
        return prev?.Prices;
    }
}
