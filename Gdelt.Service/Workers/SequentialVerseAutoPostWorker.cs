using GdeltSearchUI;

namespace Gdelt.Service.Workers;

// "The Bible, In Order" — fires every hour, 24/7. PostIfNeededAsync's own
// per-hour slot guard makes an extra tick (e.g. right after a restart) a safe
// no-op, and it stops itself once the crawl reaches Revelation 22:21.
internal sealed class SequentialVerseAutoPostWorker : PeriodicAutoPostWorker
{
    private bool _warnedMissingCreds;
    private bool _loggedComplete;

    public SequentialVerseAutoPostWorker(ILogger<SequentialVerseAutoPostWorker> logger)
        : base(logger, "bibleinorder", TimeSpan.FromHours(1)) { }

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await SequentialVerseAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case SequentialVerseOutcome.Posted:
                Logger.LogInformation("BibleInOrder: posted {Reference} ({Ordinal}/{Total})",
                    result.Reference, result.Ordinal, GdeltSearchUI.BibleBooks.TotalVerses);
                TrackTickOutcome(true);
                break;
            case SequentialVerseOutcome.AlreadyPostedThisHour:
                Logger.LogInformation("BibleInOrder: already posted this hour, skipping");
                TrackTickOutcome(true);
                break;
            case SequentialVerseOutcome.Complete:
                if (!_loggedComplete)
                {
                    Logger.LogInformation("BibleInOrder: crawl complete — {Ordinal} verses posted. Worker idle.",
                        result.Ordinal);
                    _loggedComplete = true;
                }
                TrackTickOutcome(true);
                break;
            case SequentialVerseOutcome.MissingCredentials:
                if (!_warnedMissingCreds)
                {
                    Logger.LogWarning("BibleInOrder: no Bluesky credentials — widget disabled until configured");
                    _warnedMissingCreds = true;
                }
                break;
            case SequentialVerseOutcome.Failed:
                Logger.LogError("BibleInOrder: post failed — {Error}", result.ErrorMessage);
                TrackTickOutcome(false, $"{result.Reference ?? "chapter fetch"} — {result.ErrorMessage}");
                break;
        }
    }
}
