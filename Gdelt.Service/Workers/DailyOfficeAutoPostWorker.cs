using GdeltSearchUI;

namespace Gdelt.Service.Workers;

// "The Daily Office" — fires hourly. Posts Morning Prayer once per day at/after
// 07:00 and Evening Prayer once per day at/after 18:00. PostIfNeededAsync's own
// per-day slot guards make extra ticks safe no-ops; at most one office goes out
// per tick so the two never land back-to-back after a midday start.
internal sealed class DailyOfficeAutoPostWorker : PeriodicAutoPostWorker
{
    private static readonly TimeOnly MorningAfter = new(7, 0);
    private static readonly TimeOnly EveningAfter = new(18, 0);

    private bool _warnedMissingCreds;

    public DailyOfficeAutoPostWorker(ILogger<DailyOfficeAutoPostWorker> logger)
        : base(logger, "dailyoffice", TimeSpan.FromHours(1)) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var nowT = TimeOnly.FromDateTime(DateTime.Now);
        var postedThisTick = false;

        if (nowT >= MorningAfter)
            postedThisTick = await TryPost(Office.Morning, ct);

        if (!postedThisTick && nowT >= EveningAfter)
            await TryPost(Office.Evening, ct);
    }

    // Returns true only when a fresh post went out this tick.
    private async Task<bool> TryPost(Office office, CancellationToken ct)
    {
        var result = await DailyOfficeAutoPost.PostIfNeededAsync(office, ct);
        switch (result.Outcome)
        {
            case DailyOfficeOutcome.Posted:
                Logger.LogInformation("DailyOffice: posted {Summary}", result.Summary);
                TrackTickOutcome(true);
                return true;
            case DailyOfficeOutcome.AlreadyPosted:
                TrackTickOutcome(true);
                return false;
            case DailyOfficeOutcome.MissingCredentials:
                if (!_warnedMissingCreds)
                {
                    Logger.LogWarning("DailyOffice: no Bluesky credentials — widget disabled until configured");
                    _warnedMissingCreds = true;
                }
                return false;
            case DailyOfficeOutcome.Failed:
                Logger.LogError("DailyOffice: {Office} post failed — {Error}", result.Office, result.ErrorMessage);
                TrackTickOutcome(false, $"{result.Office} — {result.ErrorMessage}");
                return false;
            default:
                return false;
        }
    }
}
