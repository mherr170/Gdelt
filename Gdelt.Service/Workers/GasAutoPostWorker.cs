using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class GasAutoPostWorker : PeriodicAutoPostWorker
{
    private readonly TimeSpan _gateInterval;

    public GasAutoPostWorker(ILogger<GasAutoPostWorker> logger, IConfiguration config)
        : base(logger, "gas", TimeSpan.FromHours(1))
    {
        _gateInterval = TimeSpan.FromHours(config.GetValue<double>("AutoPost:Gas:CheckEveryHours", 24.0));
    }

    protected override void LogStarted() =>
        Logger.LogInformation("GasAutoPostWorker started. Poll: 1h | Gate: {Gate}", _gateInterval);

    protected override bool ShouldTickNow()
    {
        var last = GasPricePostTracker.GetLastPostedAt();
        return last is null || DateTime.Now - last.Value >= _gateInterval;
    }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await GasAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case GasAutoPostOutcome.Posted:
                Logger.LogInformation("Gas: posted for period {Period}", result.Period);
                break;
            case GasAutoPostOutcome.AlreadyPosted:
                Logger.LogInformation("Gas: already posted for {Period}, skipping", result.Period);
                break;
            case GasAutoPostOutcome.MissingApiKey:
                Logger.LogWarning("Gas: no EIA API key configured — open the UI to add it");
                break;
            case GasAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("Gas: no Bluesky credentials configured for period {Period}", result.Period);
                break;
            case GasAutoPostOutcome.Failed:
                Logger.LogError("Gas: post failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
