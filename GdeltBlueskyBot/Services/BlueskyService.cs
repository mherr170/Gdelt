using System.Text;
using FishyFlip;
using FishyFlip.Lexicon;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Lexicon.App.Bsky.Richtext;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GdeltBlueskyBot.Services;

public sealed class BlueskyService : IDisposable
{
    private readonly ATProtocol _atProto;
    private readonly string _handle;
    private readonly string _appPassword;
    private readonly ILogger<BlueskyService> _logger;
    private bool _authenticated;

    public BlueskyService(IConfiguration configuration, ILogger<BlueskyService> logger)
    {
        _logger = logger;
        _handle = configuration["Bluesky:Handle"]
            ?? throw new InvalidOperationException("Bluesky:Handle is not configured.");
        _appPassword = configuration["Bluesky:AppPassword"]
            ?? throw new InvalidOperationException("Bluesky:AppPassword is not configured.");

        var instanceUrl = configuration["Bluesky:InstanceUrl"] ?? "https://bsky.social";

        _atProto = new ATProtocolBuilder()
            .WithInstanceUrl(new Uri(instanceUrl))
            .EnableAutoRenewSession(true)
            .Build();
    }

    public async Task EnsureAuthenticatedAsync(CancellationToken ct = default)
    {
        if (_authenticated) return;

        _logger.LogInformation("Authenticating with Bluesky as {Handle}", _handle);

        var result = await _atProto.AuthenticateWithPasswordResultAsync(_handle, _appPassword, null, ct);

        if (!result.IsT0)
        {
            var err = result.AsT1;
            throw new InvalidOperationException(
                $"Bluesky authentication failed: {err.Detail?.Message ?? err.ToString()}");
        }

        _authenticated = true;
        _logger.LogInformation("Bluesky authentication succeeded");
    }

    public async Task PostArticleAsync(string title, string url, CancellationToken ct = default)
    {
        await EnsureAuthenticatedAsync(ct);

        var postText = BuildPostText(title, url);
        var facets = BuildLinkFacets(postText, url);

        _logger.LogInformation("Posting to Bluesky: {Text}", postText);

        var post = new Post
        {
            Text = postText,
            Facets = facets,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _atProto.CreatePostAsync(post, cancellationToken: ct);

        if (!result.IsT0)
        {
            var err = result.AsT1;
            _logger.LogWarning("Post failed: {Error}", err.Detail?.Message ?? err.ToString());
        }
        else
        {
            _logger.LogInformation("Posted successfully: {Uri}", result.AsT0?.Uri);
        }
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static string BuildPostText(string title, string url)
    {
        const string tags = "\n\n#GDELT #News";
        int reserved = 1 + Encoding.UTF8.GetByteCount(url) + tags.Length + 5;
        var truncated = TruncateToGraphemes(title, 300 - reserved);
        return $"{truncated}\n{url}{tags}";
    }

    private static string TruncateToGraphemes(string text, int max)
    {
        if (text.Length <= max) return text;
        return text[..(max - 1)] + "…";
    }

    // Bluesky facets use UTF-8 byte offsets, not character offsets.
    private static List<Facet> BuildLinkFacets(string fullText, string url)
    {
        var fullBytes = Encoding.UTF8.GetBytes(fullText);
        var urlBytes = Encoding.UTF8.GetBytes(url);

        long urlByteStart = IndexOfSequence(fullBytes, urlBytes);
        if (urlByteStart < 0) return [];

        return
        [
            new Facet
            {
                Index = new ByteSlice
                {
                    ByteStart = urlByteStart,
                    ByteEnd = urlByteStart + urlBytes.Length,
                },
                Features = [new Link { Uri = url }],
            }
        ];
    }

    private static long IndexOfSequence(byte[] source, byte[] pattern)
    {
        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    public void Dispose() => _atProto.Dispose();
}
