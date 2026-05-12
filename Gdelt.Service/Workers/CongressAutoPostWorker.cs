using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class CongressAutoPostWorker : PeriodicAutoPostWorker
{
    private bool _warnedMissingKey;
    private bool _warnedMissingCreds;

    public CongressAutoPostWorker(ILogger<CongressAutoPostWorker> logger, IConfiguration config)
        : base(logger, "congress", TimeSpan.FromMinutes(config.GetValue<double>("AutoPost:Congress:CheckEveryMinutes", 30.0))) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await CongressAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case CongressAutoPostOutcome.Posted:
                Logger.LogInformation("Congress: posted vote {Key}", result.PostedKey);
                break;
            case CongressAutoPostOutcome.NoNewVotes:
                Logger.LogInformation("Congress: no new interesting votes");
                break;
            case CongressAutoPostOutcome.MissingApiKey:
                if (!_warnedMissingKey)
                {
                    Logger.LogWarning("Congress: no ProPublica API key — widget disabled until key is added");
                    _warnedMissingKey = true;
                }
                break;
            case CongressAutoPostOutcome.MissingCredentials:
                if (!_warnedMissingCreds)
                {
                    Logger.LogWarning("Congress: no Bluesky credentials configured");
                    _warnedMissingCreds = true;
                }
                break;
            case CongressAutoPostOutcome.Failed:
                Logger.LogError("Congress: post failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
