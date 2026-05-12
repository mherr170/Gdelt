using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class QuakeAutoPostWorker : PeriodicAutoPostWorker
{
    public QuakeAutoPostWorker(ILogger<QuakeAutoPostWorker> logger, IConfiguration config)
        : base(logger, "quake", TimeSpan.FromHours(config.GetValue<double>("AutoPost:Quake:CheckEveryHours", 1.0))) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await QuakeAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case QuakeAutoPostOutcome.Posted:
                Logger.LogInformation("Quake: posted {Count} new event(s)", result.PostedCount);
                break;
            case QuakeAutoPostOutcome.NoNewQuakes:
                Logger.LogInformation("Quake: no new M5+ events — {Count} checked", result.PostedCount);
                break;
            case QuakeAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("Quake: no Bluesky credentials configured");
                break;
            case QuakeAutoPostOutcome.Failed:
                Logger.LogError("Quake: check failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
