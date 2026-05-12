using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class WeatherAutoPostWorker : PeriodicAutoPostWorker
{
    public WeatherAutoPostWorker(ILogger<WeatherAutoPostWorker> logger)
        : base(logger, "weather", TimeSpan.FromMinutes(10)) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await WeatherAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case WeatherAutoPostOutcome.Posted:
                Logger.LogInformation("Weather: posted {Count} alert(s)", result.PostedCount);
                break;
            case WeatherAutoPostOutcome.NoNewAlerts:
                Logger.LogInformation("Weather: no new high-impact alerts");
                break;
            case WeatherAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("Weather: no Bluesky credentials configured");
                break;
            case WeatherAutoPostOutcome.Failed:
                Logger.LogError("Weather: post failed — {Error}", result.ErrorMessage);
                break;
        }
    }
}
