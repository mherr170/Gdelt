using SkiaSharp;

namespace GdeltSearchUI;

/// <summary>
/// Renders a regional context map for an earthquake post: epicenter as a red
/// circle (sized by magnitude) plus nearby recent quakes as gray dots, on top
/// of OpenStreetMap raster tiles.
/// </summary>
internal static class QuakeMap
{
    private const int TileSize = 256;

    public static async Task<byte[]> RenderPngAsync(
        QuakeEvent epicenter, IReadOnlyList<QuakeEvent> nearby,
        int widthTiles = 3, int heightTiles = 2, int zoom = 6,
        CancellationToken ct = default)
    {
        if (epicenter.Latitude is null || epicenter.Longitude is null) return [];
        var lat = epicenter.Latitude.Value;
        var lon = epicenter.Longitude.Value;

        // Center tile in fractional coordinates so the epicenter sits in the middle.
        var (cxF, cyF) = LatLonToTile(lat, lon, zoom);

        var leftTile = (int)Math.Floor(cxF - widthTiles  / 2.0);
        var topTile  = (int)Math.Floor(cyF - heightTiles / 2.0);

        var w = widthTiles  * TileSize;
        var h = heightTiles * TileSize;

        using var surface = SKSurface.Create(new SKImageInfo(w, h));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0x2A, 0x2A, 0x2A));

        // Composite tiles in parallel.
        var tasks = new List<Task<(int dx, int dy, SKBitmap? tile)>>();
        for (var ty = 0; ty < heightTiles; ty++)
        for (var tx = 0; tx < widthTiles; tx++)
        {
            var z = zoom; var X = leftTile + tx; var Y = topTile + ty;
            var dx = tx * TileSize; var dy = ty * TileSize;
            tasks.Add(Task.Run(async () =>
            {
                var tile = await MapTileFetcher.GetTileAsync(z, X, Y, ct);
                return (dx, dy, tile);
            }, ct));
        }
        var tiles = await Task.WhenAll(tasks);
        foreach (var (dx, dy, tile) in tiles)
            if (tile is not null) canvas.DrawBitmap(tile, dx, dy);

        // Helper: lat/lon → pixel within composite image.
        SKPoint Project(double la, double lo)
        {
            var (fx, fy) = LatLonToTile(la, lo, zoom);
            return new SKPoint(
                (float)((fx - leftTile) * TileSize),
                (float)((fy - topTile)  * TileSize));
        }

        // Nearby quakes (gray, sized by magnitude).
        using var nearPaint = new SKPaint
        {
            Color = new SKColor(0xCC, 0xCC, 0xCC, 0xC0),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var nearStroke = new SKPaint
        {
            Color = new SKColor(0x33, 0x33, 0x33),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };
        foreach (var q in nearby)
        {
            if (q.Id == epicenter.Id) continue;
            if (q.Latitude is null || q.Longitude is null) continue;
            var p = Project(q.Latitude.Value, q.Longitude.Value);
            var r = MagToRadius(q.Magnitude) * 0.6f;
            canvas.DrawCircle(p, r, nearPaint);
            canvas.DrawCircle(p, r, nearStroke);
        }

        // Epicenter (red, larger).
        var epi = Project(lat, lon);
        using var epiHalo = new SKPaint
        {
            Color = new SKColor(0xE5, 0x4B, 0x4B, 0x40),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var epiFill = new SKPaint
        {
            Color = new SKColor(0xE5, 0x4B, 0x4B),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };
        using var epiStroke = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntialias = true,
        };
        var epiR = MagToRadius(epicenter.Magnitude);
        canvas.DrawCircle(epi, epiR * 2.2f, epiHalo);
        canvas.DrawCircle(epi, epiR, epiFill);
        canvas.DrawCircle(epi, epiR, epiStroke);

        // Title + attribution.
        DrawTextBar(canvas, w, h, $"M {epicenter.Magnitude:F1} — {epicenter.Place}");
        DrawAttribution(canvas, w, h);

        using var image = surface.Snapshot();
        using var data  = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static float MagToRadius(double mag) =>
        Math.Max(4f, (float)(Math.Pow(2.0, mag - 2.0) * 1.2));

    // Web Mercator: lat/lon → fractional tile (z, x, y).
    private static (double X, double Y) LatLonToTile(double lat, double lon, int z)
    {
        var n   = Math.Pow(2, z);
        var x   = (lon + 180.0) / 360.0 * n;
        var rad = lat * Math.PI / 180.0;
        var y   = (1.0 - Math.Log(Math.Tan(rad) + 1.0 / Math.Cos(rad)) / Math.PI) / 2.0 * n;
        return (x, y);
    }

    private static void DrawTextBar(SKCanvas canvas, int w, int h, string title)
    {
        using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 0xB0), Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, 0, w, 28, bg);

        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 14);
        using var paint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawText(title, 8, 20, SKTextAlign.Left, font, paint);
    }

    private static void DrawAttribution(SKCanvas canvas, int w, int h)
    {
        using var bg = new SKPaint { Color = new SKColor(0, 0, 0, 0xB0), Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, h - 18, w, 18, bg);

        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
        using var paint = new SKPaint { Color = new SKColor(0xDD, 0xDD, 0xDD), IsAntialias = true };
        canvas.DrawText("© OpenStreetMap contributors © CARTO  ·  Quakes: USGS", 8, h - 5, SKTextAlign.Left, font, paint);
    }
}
