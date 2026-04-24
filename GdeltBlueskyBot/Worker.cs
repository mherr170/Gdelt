using GdeltBlueskyBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GdeltBlueskyBot;

public sealed class Worker(
    GdeltClient gdeltClient,
    BlueskyService blueskyService,
    PostedArticlesRepository repository,
    IConfiguration configuration,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly int _articlesPerCycle =
        configuration.GetValue("Gdelt:ArticlesPerCycle", 2);

    private readonly TimeSpan _pollingInterval =
        TimeSpan.FromMinutes(configuration.GetValue("Bot:PollingIntervalMinutes", 15));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repository.InitializeAsync(stoppingToken);

        // Run immediately on startup, then on the interval.
        await RunCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(_pollingInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        logger.LogInformation("--- Starting GDELT cycle ---");

        var articles = await gdeltClient.FetchArticlesAsync(ct);
        if (articles.Count == 0)
        {
            logger.LogWarning("No articles returned this cycle");
            return;
        }

        var candidates = GdeltClient.SelectTopArticles(articles, _articlesPerCycle * 4);
        int posted = 0;

        foreach (var article in candidates)
        {
            if (posted >= _articlesPerCycle) break;
            if (string.IsNullOrWhiteSpace(article.Url) || string.IsNullOrWhiteSpace(article.Title))
                continue;

            if (await repository.HasBeenPostedAsync(article.Url, ct))
            {
                logger.LogDebug("Skipping already-posted article: {Url}", article.Url);
                continue;
            }

            try
            {
                await blueskyService.PostArticleAsync(article.Title, article.Url, ct);
                await repository.MarkAsPostedAsync(article.Url, ct);
                posted++;

                // Brief delay between posts to be a polite API citizen.
                if (posted < _articlesPerCycle)
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to post article {Url}", article.Url);
            }
        }

        logger.LogInformation("Cycle complete — posted {Count} article(s)", posted);
    }
}
