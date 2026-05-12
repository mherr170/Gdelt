using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class ApodAutoPostWorker : PeriodicAutoPostWorker
{
    private bool _warnedMissingKey;
    private bool _warnedMissingCreds;

    public ApodAutoPostWorker(ILogger<ApodAutoPostWorker> logger)
        : base(logger, "apod", TimeSpan.FromHours(1)) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await ApodAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case ApodAutoPostOutcome.Posted:
                Logger.LogInformation("APOD: posted for {Date}", result.Date);
                break;
            case ApodAutoPostOutcome.AlreadyPosted:
                Logger.LogInformation("APOD: already posted for {Date}, skipping", result.Date);
                break;
            case ApodAutoPostOutcome.MissingApiKey:
                if (!_warnedMissingKey)
                {
                    Logger.LogWarning("APOD: no NASA API key — widget disabled until key is added");
                    _warnedMissingKey = true;
                }
                break;
            case ApodAutoPostOutcome.MissingCredentials:
                if (!_warnedMissingCreds)
                {
                    Logger.LogWarning("APOD: no Bluesky credentials configured for {Date}", result.Date);
                    _warnedMissingCreds = true;
                }
                break;
            case ApodAutoPostOutcome.Failed:
                Logger.LogError("APOD: post failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
