using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class YahooAutoPostWorker : BackgroundService
{
    private readonly ILogger<YahooAutoPostWorker> _logger;
    private readonly TimeOnly[] _scheduledTimes;

    public YahooAutoPostWorker(ILogger<YahooAutoPostWorker> logger, IConfiguration config)
    {
        _logger = logger;

        var raw = config.GetSection("AutoPost:Yahoo:ScheduledTimes").Get<string[]>()
                  ?? ["08:00", "13:00", "19:00"];

        _scheduledTimes = raw
            .Select(s => TimeOnly.Parse(s))
            .OrderBy(t => t)
            .ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "YahooAutoPostWorker started. Scheduled times: {Times}",
            string.Join(", ", _scheduledTimes.Select(t => t.ToString("HH:mm"))));
        _ = PostLogger.ClearPeriodicallyAsync("yahoo", TimeSpan.FromHours(48), stoppingToken);

        // Poll every minute so sleep/hibernate doesn't cause missed slots.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (ShouldPostNow())
                await RunOnceAsync(stoppingToken);
        }
    }

    // Returns true if any scheduled slot for today has passed and hasn't been posted yet.
    private bool ShouldPostNow()
    {
        var now      = DateTime.Now;
        var lastPost = YahooPostTracker.GetLastPostedAt();

        foreach (var t in _scheduledTimes)
        {
            var slot = DateTime.Today.Add(t.ToTimeSpan());
            if (now >= slot && (lastPost is null || lastPost.Value < slot))
                return true;
        }
        return false;
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            var result = await YahooAutoPost.PostIfNeededAsync(ct);
            switch (result.Outcome)
            {
                case YahooAutoPostOutcome.Posted:
                    _logger.LogInformation("Yahoo: posted at {Time}", result.LastPostedAt);
                    break;
                case YahooAutoPostOutcome.MissingCredentials:
                    _logger.LogWarning("Yahoo: no Bluesky credentials configured");
                    break;
                case YahooAutoPostOutcome.Failed:
                    _logger.LogError("Yahoo: post failed — {Error}", result.ErrorMessage);
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yahoo: unhandled exception in worker");
        }
    }
}
