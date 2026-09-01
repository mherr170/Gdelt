using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal abstract class PeriodicAutoPostWorker : BackgroundService
{
    private readonly ILogger _logger;
    private readonly string  _tag;
    private readonly TimeSpan _interval;

    private int _consecutiveFailures;

    // After this many back-to-back failed ticks a widget is almost certainly
    // stuck (repeated failures never advance its state), so escalate from the
    // per-tick ERROR to one loud WARN — and repeat it every threshold-worth of
    // ticks after that so it stays visible to /widget-health.
    private const int FailureAlertThreshold = 6;

    protected ILogger Logger => _logger;

    protected PeriodicAutoPostWorker(ILogger logger, string tag, TimeSpan interval)
    {
        _logger   = logger;
        _tag      = tag;
        _interval = interval;
    }

    protected abstract Task TickAsync(CancellationToken ct);

    // Gas/Debt override this to gate on last-post age rather than running every tick.
    protected virtual bool ShouldTickNow() => true;

    // Workers with non-standard startup messages (e.g. two intervals) can override.
    protected virtual void LogStarted() =>
        _logger.LogInformation("{Worker} started. Interval: {Interval}", GetType().Name, _interval);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted();
        _ = PostLogger.ClearPeriodicallyAsync(_tag, TimeSpan.FromHours(48), stoppingToken);

        await RunSafeAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            if (ShouldTickNow())
                await RunSafeAsync(stoppingToken);
    }

    protected async Task RunSafeAsync(CancellationToken ct)
    {
        try { await TickAsync(ct); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _logger.LogError(ex, "{Tag}: unhandled exception in worker", _tag); }
    }

    // Opt-in: workers whose state only advances on a successful post call this
    // once per tick so a silent, self-perpetuating failure (bad chapter fetch,
    // a verse Bluesky keeps rejecting) gets surfaced instead of just spamming
    // ERROR forever. `succeeded` should be true for any non-failure outcome
    // (posted, nothing-to-do, already-done); `context` describes the stuck item.
    protected void TrackTickOutcome(bool succeeded, string context = "")
    {
        if (succeeded)
        {
            if (_consecutiveFailures >= FailureAlertThreshold)
            {
                _logger.LogInformation("{Tag}: recovered after {Count} consecutive failed ticks", _tag, _consecutiveFailures);
                PostLogger.Info(_tag, $"Recovered after {_consecutiveFailures} consecutive failed ticks");
            }
            _consecutiveFailures = 0;
            return;
        }

        _consecutiveFailures++;
        if (_consecutiveFailures < FailureAlertThreshold ||
            _consecutiveFailures % FailureAlertThreshold != 0)
            return;

        var stuckFor = TimeSpan.FromTicks(_interval.Ticks * _consecutiveFailures);
        var msg = $"STUCK — {_consecutiveFailures} consecutive failed ticks (~{stuckFor.TotalHours:F0}h), no progress" +
                  (string.IsNullOrEmpty(context) ? "" : $". Last: {context}");
        _logger.LogWarning("{Tag}: {Message}", _tag, msg);
        PostLogger.Warn(_tag, msg);
    }
}
