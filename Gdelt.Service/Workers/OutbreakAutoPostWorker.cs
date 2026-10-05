using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class OutbreakAutoPostWorker : PeriodicAutoPostWorker
{
    public OutbreakAutoPostWorker(ILogger<OutbreakAutoPostWorker> logger, IConfiguration config)
        : base(logger, "outbreak", TimeSpan.FromHours(config.GetValue<double>("AutoPost:Outbreak:CheckEveryHours", 2.0))) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await OutbreakAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case OutbreakAutoPostOutcome.Posted:
                Logger.LogInformation("Outbreak: posted {Count} article(s)", result.PostedCount);
                break;
            case OutbreakAutoPostOutcome.NoNewArticles:
                Logger.LogInformation("Outbreak: no new articles passed filters");
                break;
            case OutbreakAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("Outbreak: no Bluesky credentials configured");
                break;
            case OutbreakAutoPostOutcome.Failed:
                Logger.LogError("Outbreak: check failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
