using SkiaSharp;

namespace GdeltSearchUI;

/// <summary>
/// Fetches dark-themed raster map tiles for the quake context map. Uses Esri's
/// "Dark Gray Canvas" (base + reference/label layer composited together) — it is
/// keyless for low-volume use and matches the app's dark palette. Identifies the
/// app via User-Agent and caches composited tiles in-memory.
/// </summary>
internal static class MapTileFetcher
{
    // Esri ArcGIS Online "Dark Gray Canvas". NOTE: Esri tile URLs are
    // {z}/{row}/{col} = {z}/{y}/{x}, not the XYZ {z}/{x}/{y} order.
    // The base is landmass/water only; the reference layer adds place labels
    // and admin boundaries on a transparent background.
    private const string BaseUrl =
        "https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{0}/{1}/{2}";
    private const string ReferenceUrl =
        "https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Reference/MapServer/tile/{0}/{1}/{2}";
    private const string UserAgent = "GdeltSearchUI/1.0 (+https://github.com/mherr170)";

    private static readonly HttpClient _http;
    private static readonly Dictionary<(int z, int x, int y), SKBitmap> _cache = new();
    private static readonly object _lock = new();

    static MapTileFetcher()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    public static async Task<SKBitmap?> GetTileAsync(int z, int x, int y, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue((z, x, y), out var cached)) return cached;
        }

        // Esri expects {z}/{y}/{x}.
        var baseBmp = await FetchAsync(string.Format(BaseUrl, z, y, x), ct);
        if (baseBmp is null) return null;

        var refBmp = await FetchAsync(string.Format(ReferenceUrl, z, y, x), ct);
        var composed = refBmp is null ? baseBmp : Compose(baseBmp, refBmp);

        lock (_lock) _cache[(z, x, y)] = composed;
        return composed;
    }

    private static async Task<SKBitmap?> FetchAsync(string url, CancellationToken ct)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            return SKBitmap.Decode(bytes);
        }
        catch
        {
            return null;
        }
    }

    // Draw the label/boundary layer over the base tile into a single bitmap.
    private static SKBitmap Compose(SKBitmap baseBmp, SKBitmap overlay)
    {
        var result = new SKBitmap(baseBmp.Width, baseBmp.Height);
        using (var canvas = new SKCanvas(result))
        {
            canvas.DrawBitmap(baseBmp, 0, 0);
            var dest = new SKRect(0, 0, baseBmp.Width, baseBmp.Height);
            canvas.DrawBitmap(overlay, dest);
        }
        baseBmp.Dispose();
        overlay.Dispose();
        return result;
    }
}
