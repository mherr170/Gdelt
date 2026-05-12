using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class DebtAutoPostWorker : PeriodicAutoPostWorker
{
    private readonly TimeSpan _gateInterval;

    public DebtAutoPostWorker(ILogger<DebtAutoPostWorker> logger, IConfiguration config)
        : base(logger, "debt", TimeSpan.FromHours(1))
    {
        _gateInterval = TimeSpan.FromHours(config.GetValue<double>("AutoPost:Debt:CheckEveryHours", 24.0));
    }

    protected override void LogStarted() =>
        Logger.LogInformation("DebtAutoPostWorker started. Poll: 1h | Gate: {Gate}", _gateInterval);

    protected override bool ShouldTickNow()
    {
        var last = DebtPostTracker.GetLastPostedAt();
        return last is null || DateTime.Now - last.Value >= _gateInterval;
    }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await DebtAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case DebtAutoPostOutcome.Posted:
                Logger.LogInformation("Debt: posted for record date {Date}", result.RecordDate);
                break;
            case DebtAutoPostOutcome.AlreadyPosted:
                Logger.LogInformation("Debt: already posted for {Date}, skipping", result.RecordDate);
                break;
            case DebtAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("Debt: no Bluesky credentials configured for {Date}", result.RecordDate);
                break;
            case DebtAutoPostOutcome.Failed:
                Logger.LogError("Debt: post failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
