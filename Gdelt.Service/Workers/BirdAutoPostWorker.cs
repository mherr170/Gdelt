using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class BirdAutoPostWorker : BackgroundService
{
    private readonly ILogger<BirdAutoPostWorker> _logger;
    private readonly TimeOnly[] _scheduledTimes;

    public BirdAutoPostWorker(ILogger<BirdAutoPostWorker> logger, IConfiguration config)
    {
        _logger = logger;

        var raw = config.GetSection("AutoPost:Bird:ScheduledTimes").Get<string[]>()
                  ?? ["08:00", "18:00"];

        _scheduledTimes = raw
            .Select(s => TimeOnly.Parse(s))
            .OrderBy(t => t)
            .ToArray();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BirdAutoPostWorker started. Scheduled times: {Times}",
            string.Join(", ", _scheduledTimes.Select(t => t.ToString("HH:mm"))));
        _ = PostLogger.ClearPeriodicallyAsync("bird", TimeSpan.FromHours(48), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var slot = CurrentDueSlot();
            if (slot is not null)
                await RunOnceAsync(slot, stoppingToken);
        }
    }

    // Returns the slot key for the first scheduled time that has passed today and not yet posted.
    private string? CurrentDueSlot()
    {
        var now = DateTime.Now;
        foreach (var t in _scheduledTimes)
        {
            var slotKey = BirdAutoPost.SlotKey(now, t);
            if (now >= DateTime.Today.Add(t.ToTimeSpan()) && !BirdPostTracker.HasBeenPosted(slotKey))
                return slotKey;
        }
        return null;
    }

    private async Task RunOnceAsync(string slotKey, CancellationToken ct)
    {
        try
        {
            var result = await BirdAutoPost.PostIfNeededAsync(slotKey, ct);
            switch (result.Outcome)
            {
                case BirdAutoPostOutcome.Posted:
                    _logger.LogInformation("Bird: posted {Count} video(s) for slot {Slot}", result.PostedCount, slotKey);
                    break;
                case BirdAutoPostOutcome.AlreadyPosted:
                    _logger.LogInformation("Bird: already posted for slot {Slot}", slotKey);
                    break;
                case BirdAutoPostOutcome.MissingApiKey:
                    _logger.LogWarning("Bird: no YouTube API key configured");
                    break;
                case BirdAutoPostOutcome.MissingCredentials:
                    _logger.LogWarning("Bird: no Bluesky credentials configured");
                    break;
                case BirdAutoPostOutcome.NoVideosFound:
                    _logger.LogWarning("Bird: no videos returned from YouTube API");
                    break;
                case BirdAutoPostOutcome.Failed:
                    _logger.LogError("Bird: post failed — {Error}", result.ErrorMessage);
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bird: unhandled exception in worker");
        }
    }
}
