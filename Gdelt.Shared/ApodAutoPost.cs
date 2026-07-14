using SkiaSharp;

namespace GdeltSearchUI;

internal enum ApodAutoPostOutcome { Posted, AlreadyPosted, MissingCredentials, MissingApiKey, Failed }

internal sealed record ApodAutoPostResult(
    ApodAutoPostOutcome Outcome,
    string?             Date         = null,
    string?             ErrorMessage = null);

internal static class ApodAutoPost
{
    private const string W          = "apod";
    private const int    MaxBytes   = 950_000; // Bluesky image limit is 1MB

    public static async Task<ApodAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        var apiKey = CredentialManager.LoadNasaApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PostLogger.Warn(W, "No NASA API key configured");
            return new(ApodAutoPostOutcome.MissingApiKey);
        }

        PostLogger.Info(W, "Fetching today's NASA APOD…");
        ApodEntry? entry;
        try
        {
            using var client = new ApodApiClient(apiKey);
            entry = await client.GetTodayAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"Fetch failed: {ex.Message}");
            return new(ApodAutoPostOutcome.Failed, ErrorMessage: ex.Message);
        }

        if (entry is null)
        {
            PostLogger.Error(W, "No APOD entry returned");
            return new(ApodAutoPostOutcome.Failed, ErrorMessage: "No data returned from NASA API.");
        }

        PostLogger.Info(W, $"Today's APOD: \"{entry.Title}\" ({entry.Date}){(entry.IsVideo ? " [video]" : "")}");

        if (ApodPostTracker.HasBeenPosted(entry.Date))
        {
            PostLogger.Info(W, $"Already posted for {entry.Date} — skipping");
            return new(ApodAutoPostOutcome.AlreadyPosted, entry.Date);
        }

        var creds = CredentialManager.LoadApodBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(ApodAutoPostOutcome.MissingCredentials, entry.Date);
        }

        var (headline, tags) = await LmStudioPostGenerator.GenerateApodPostAsync(entry);
        var text = BuildPostText(entry, headline, tags);

        using var poster = new BlueskyPoster();
        (bool Ok, string? Error) result;

        if (entry.IsVideo && !string.IsNullOrEmpty(entry.VideoUrl))
        {
            // Post video APODs as a clickable link card so followers can watch
            var thumb = entry.ImageUrl is not null ? await TryDownloadImageAsync(entry.ImageUrl, ct) : null;
            if (thumb is { Length: > 0 })
            {
                result = await poster.PostExternalLinkAsync(
                    creds.Value.Handle, creds.Value.Password,
                    text:            text,
                    linkUri:         entry.VideoUrl,
                    cardTitle:       entry.Title,
                    cardDescription: headline,
                    thumbBytes:      thumb,
                    ct);
            }
            else
            {
                // No thumbnail — embed the video link directly in the post text
                PostLogger.Info(W, "No thumbnail available — including video link in post text");
                var textWithLink = BuildPostText(entry, headline, tags, entry.VideoUrl);
                result = await poster.PostTextWithLinkAsync(
                    creds.Value.Handle, creds.Value.Password, textWithLink, entry.VideoUrl, ct);
            }
        }
        else
        {
            var imageBytes = await TryDownloadImageAsync(entry.ImageUrl, ct);
            if (imageBytes.Length > 0)
            {
                var alt = BuildAltText(entry, headline);
                result = await poster.PostTextWithImageAsync(
                    creds.Value.Handle, creds.Value.Password, text, imageBytes, alt, ct);
            }
            else
            {
                PostLogger.Warn(W, "Image unavailable — posting text only");
                result = await poster.PostTextAsync(creds.Value.Handle, creds.Value.Password, text, ct);
            }
        }

        if (result.Ok)
        {
            ApodPostTracker.MarkPosted(entry.Date);
            PostLogger.Success(W, $"Posted: \"{entry.Title}\" | {entry.Date}");
            return new(ApodAutoPostOutcome.Posted, entry.Date);
        }

        PostLogger.Error(W, $"Post failed: {result.Error}");
        return new(ApodAutoPostOutcome.Failed, entry.Date, result.Error);
    }

    internal static string BuildPostText(ApodEntry entry, string headline, string[] tags, string? videoUrl = null)
    {
        var creditLine  = entry.Copyright is not null ? $"\n© {entry.Copyright}" : "";
        var videoNote   = entry.IsVideo ? " 🎬" : "";
        var allTags     = tags.Prepend("Astronomy").Prepend("APOD").Prepend("NASA").Distinct().ToArray();
        var hashtagLine = BlueskyPostHelper.HashtagLine(allTags);
        var linkLine    = videoUrl is not null ? $"\n{videoUrl}" : "";

        return
            $"🔭 {entry.Title}{videoNote}\n\n" +
            $"📅 {entry.Date}" +
            creditLine +
            $"\nSrc: NASA Astronomy Picture of the Day" +
            linkLine +
            hashtagLine;
    }

    private static string BuildAltText(ApodEntry entry, string headline) =>
        $"NASA Astronomy Picture of the Day for {entry.Date}: {entry.Title}. {headline}";

    internal static async Task<byte[]> TryDownloadImageAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return [];

        try
        {
            using var http  = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var raw = await http.GetByteArrayAsync(url, ct);

            if (raw.Length <= MaxBytes) return raw;

            // Image too large — compress with SkiaSharp
            return CompressToJpeg(raw, MaxBytes);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Warn(W, $"Image download failed ({ex.Message}) — posting text only");
            return [];
        }
    }

    private static byte[] CompressToJpeg(byte[] raw, int maxBytes)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(raw);
            if (bitmap is null) return [];

            for (int quality = 85; quality >= 40; quality -= 15)
            {
                using var image  = SKImage.FromBitmap(bitmap);
                using var data   = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                var bytes = data.ToArray();
                if (bytes.Length <= maxBytes) return bytes;
            }

            // Still too large — shrink 0.8× per iteration until it fits
            SKBitmap? scaled = null;
            try
            {
                SKBitmap src = bitmap;
                while (true)
                {
                    int w = Math.Max(100, (int)(src.Width  * 0.8));
                    int h = Math.Max(100, (int)(src.Height * 0.8));
                    if (w >= src.Width)
                    {
                        // Already at minimum — last resort: encode at lowest quality
                        using var imgMin  = SKImage.FromBitmap(src);
                        using var dataMin = imgMin.Encode(SKEncodedImageFormat.Jpeg, 10);
                        return dataMin.ToArray();
                    }

                    var next = src.Resize(new SKImageInfo(w, h), SKFilterQuality.Medium);
                    scaled?.Dispose();
                    scaled = next;
                    if (next is null) return [];
                    src = next;

                    using var img  = SKImage.FromBitmap(src);
                    using var data = img.Encode(SKEncodedImageFormat.Jpeg, 75);
                    var bytes = data.ToArray();
                    if (bytes.Length <= maxBytes) return bytes;
                }
            }
            finally
            {
                scaled?.Dispose();
            }
        }
        catch
        {
            return [];
        }
    }
}
