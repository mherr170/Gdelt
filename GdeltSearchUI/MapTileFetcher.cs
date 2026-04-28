using SkiaSharp;

namespace GdeltSearchUI;

/// <summary>
/// Fetches OpenStreetMap raster tiles. Honors the OSM tile usage policy:
/// identifies the app via User-Agent and caches results in-memory to avoid
/// duplicate hits for the same tile.
/// </summary>
internal static class MapTileFetcher
{
    // CartoDB "Dark Matter" — OSM-derived tiles with English/Latin-script labels
    // and a dark palette that matches the app theme. Free for low-volume use;
    // attribution required (rendered into the image by QuakeMap).
    private const string TileUrl  = "https://a.basemaps.cartocdn.com/dark_all/{0}/{1}/{2}.png";
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

        try
        {
            var url   = string.Format(TileUrl, z, x, y);
            var bytes = await _http.GetByteArrayAsync(url, ct);
            var bmp   = SKBitmap.Decode(bytes);
            if (bmp is null) return null;

            lock (_lock) _cache[(z, x, y)] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
    }
}
