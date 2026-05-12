using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class StockAutoPostWorker : PeriodicAutoPostWorker
{
    private static readonly TimeZoneInfo Eastern =
        TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    public StockAutoPostWorker(ILogger<StockAutoPostWorker> logger)
        : base(logger, "stocks", TimeSpan.Zero) { }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("StockAutoPostWorker started. Posts after 4:10 PM ET on trading days.");
        _ = PostLogger.ClearPeriodicallyAsync("stocks", TimeSpan.FromHours(48), stoppingToken);

        // Check immediately — handles service restart after market close.
        await RunSafeAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNext410pmET();
            Logger.LogInformation("Stocks: sleeping {Delay:hh\\:mm} until next 4:10 PM ET check", delay);
            await Task.Delay(delay, stoppingToken);
            await RunSafeAsync(stoppingToken);
        }
    }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await StockAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case StockAutoPostOutcome.Posted:
                Logger.LogInformation("Stocks: posted close for {Date}", result.TradingDate);
                break;
            case StockAutoPostOutcome.AlreadyPosted:
                Logger.LogInformation("Stocks: already posted for {Date}", result.TradingDate);
                break;
            case StockAutoPostOutcome.MarketNotClosed:
                Logger.LogInformation("Stocks: market not closed yet or no trading today");
                break;
            case StockAutoPostOutcome.MissingCredentials:
                Logger.LogWarning("Stocks: no Bluesky credentials configured");
                break;
            case StockAutoPostOutcome.Failed:
                Logger.LogError("Stocks: post failed — {Error}", result.ErrorMessage);
                break;
        }
    }

    private static TimeSpan DelayUntilNext410pmET()
    {
        var etNow  = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Eastern);
        var cutoff = new TimeSpan(16, 10, 0); // 4:10 PM ET

        DateTime targetET;
        if (etNow.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday
            && etNow.TimeOfDay < cutoff)
        {
            // Before 4:10pm on a weekday — wait until today's slot
            targetET = etNow.Date.Add(cutoff);
        }
        else
        {
            // After 4:10pm, or weekend — advance to next weekday
            var next = etNow.Date.AddDays(1);
            while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                next = next.AddDays(1);
            targetET = next.Add(cutoff);
        }

        var targetUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(targetET, DateTimeKind.Unspecified), Eastern);

        var delay = targetUtc - DateTime.UtcNow;
        return delay > TimeSpan.FromSeconds(1) ? delay : TimeSpan.FromSeconds(1);
    }
}
