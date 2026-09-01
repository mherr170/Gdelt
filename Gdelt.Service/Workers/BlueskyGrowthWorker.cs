using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class BlueskyGrowthWorker : BackgroundService
{
    private readonly ILogger<BlueskyGrowthWorker> _logger;
    private readonly int _runAtHour;
    private readonly int _dailyFollowLimit;
    private readonly int _dailyLikeLimit;
    private readonly int _unfollowGraceDays;
    private DateTime? _lastRunDate;

    public BlueskyGrowthWorker(ILogger<BlueskyGrowthWorker> logger, IConfiguration config)
    {
        _logger            = logger;
        _runAtHour         = config.GetValue<int>("Growth:RunAtHour", 9);
        _dailyFollowLimit  = config.GetValue<int>("Growth:DailyFollowsPerAccount", 20);
        _dailyLikeLimit    = config.GetValue<int>("Growth:DailyLikesPerAccount", 20);
        _unfollowGraceDays = config.GetValue<int>("Growth:UnfollowGraceDays", 14);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BlueskyGrowthWorker started. Runs daily at {Hour:D2}:00 — {Follows} follows, {Likes} likes, unfollow after {Grace}d.",
            _runAtHour, _dailyFollowLimit, _dailyLikeLimit, _unfollowGraceDays);
        _ = PostLogger.ClearPeriodicallyAsync("growth", TimeSpan.FromHours(48), stoppingToken);

        // Run immediately on startup so today isn't missed, then follow the daily schedule.
        await RunAllAccountsAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (ShouldRunNow())
                await RunAllAccountsAsync(stoppingToken);
        }
    }

    private bool ShouldRunNow()
    {
        var now = DateTime.Now;
        if (now.Hour < _runAtHour) return false;
        if (_lastRunDate.HasValue && _lastRunDate.Value.Date == now.Date) return false;
        return true;
    }

    private async Task RunAllAccountsAsync(CancellationToken ct)
    {
        _lastRunDate = DateTime.Now;

        var accounts = BotNetworks.GrowthRoster();
        if (accounts.Count == 0)
        {
            PostLogger.Info("growth", "No Bluesky accounts configured — skipping growth run");
            return;
        }

        PostLogger.Info("growth",
            $"Starting daily growth run — {accounts.Count} account(s), " +
            $"{_dailyFollowLimit} follow(s) and {_dailyLikeLimit} like(s)/account");

        foreach (var (label, slug, handle, password) in accounts)
        {
            try
            {
                await BlueskyGrowthAutoPost.RunAsync(
                    label, slug, handle, password,
                    _dailyFollowLimit, _dailyLikeLimit, _unfollowGraceDays, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Growth: unhandled exception for {Handle}", handle);
            }
        }

        PostLogger.Info("growth", "Daily follow run complete");
    }
}
