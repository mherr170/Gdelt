using System.Net.Http.Json;
using GdeltBlueskyBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GdeltBlueskyBot.Services;

public sealed class GdeltClient(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<GdeltClient> logger)
{
    private readonly string _queryUrl = configuration["Gdelt:QueryUrl"]
        ?? throw new InvalidOperationException("Gdelt:QueryUrl is not configured.");

    public async Task<List<GdeltArticle>> FetchArticlesAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Fetching GDELT articles from {Url}", _queryUrl);

        GdeltResponse? response;
        try
        {
            response = await httpClient.GetFromJsonAsync<GdeltResponse>(_queryUrl, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error fetching GDELT data");
            return [];
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogWarning("GDELT fetch timed out or was cancelled");
            return [];
        }

        if (response?.Articles is null or { Count: 0 })
        {
            logger.LogWarning("GDELT returned an empty article list");
            return [];
        }

        logger.LogInformation("Received {Count} articles from GDELT", response.Articles.Count);
        return response.Articles;
    }

    /// <summary>
    /// Returns the top N articles ranked by absolute tone (most intense coverage first).
    /// Falls back to most-recent order when tone is absent.
    /// </summary>
    public static List<GdeltArticle> SelectTopArticles(List<GdeltArticle> articles, int count) =>
        articles
            .OrderByDescending(a => Math.Abs(a.Tone))
            .Take(count)
            .ToList();
}
