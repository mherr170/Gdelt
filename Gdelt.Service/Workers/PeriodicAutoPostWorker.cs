using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal abstract class PeriodicAutoPostWorker : BackgroundService
{
    private readonly ILogger _logger;
    private readonly string  _tag;
    private readonly TimeSpan _interval;

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
}
