using System.Text.Json;

namespace GdeltSearchUI;

/// <summary>
/// Persists a price snapshot every time a Yahoo Finance post goes out.
/// Each entry = prices at post time.  Up to 90 snapshots kept.
/// </summary>
internal static class YahooPriceCache
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "yahoo_futures_history.json");

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    private const int MaxPoints = 90;

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
            AppLogger.Log($"YahooPriceCache load failed: {ex.Message}");
            return [];
        }
    }

    public static void Append(IReadOnlyList<OilPriceEntry> prices)
    {
        var history = Load();
        var point   = new CommodityHistoryPoint
        {
            Timestamp = DateTimeOffset.Now,
            Prices    = prices.ToDictionary(e => e.Code, e => e.Price),
        };
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
            AppLogger.Log($"YahooPriceCache save failed: {ex.Message}");
        }
    }
}
