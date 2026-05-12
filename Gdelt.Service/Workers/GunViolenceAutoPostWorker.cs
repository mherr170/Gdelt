using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class GunViolenceAutoPostWorker : PeriodicAutoPostWorker
{
    public GunViolenceAutoPostWorker(ILogger<GunViolenceAutoPostWorker> logger, IConfiguration config)
        : base(logger, "gunviolence", TimeSpan.FromHours(config.GetValue<double>("AutoPost:GunViolence:CheckEveryHours", 3.0))) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await GunViolenceAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case GunViolenceAutoPostOutcome.Posted:
                Logger.LogInformation("GunViolence: posted {Count} article(s)", result.PostedCount);
                break;
            case GunViolenceAutoPostOutcome.NoNewArticles:
                Logger.LogInformation("GunViolence: no new articles passed filters");
                break;
            case GunViolenceAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("GunViolence: no Bluesky credentials configured");
                break;
            case GunViolenceAutoPostOutcome.Failed:
                Logger.LogError("GunViolence: check failed — {Error}", result.ErrorMessage);
                break;
            case GunViolenceAutoPostOutcome.RateLimited:
                Logger.LogWarning("GunViolence: rate-limited after all retries — next attempt in ~{Hours}h", 3.0);
                break;
        }
    }
}
