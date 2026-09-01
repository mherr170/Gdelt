using GdeltSearchUI;

namespace Gdelt.Service.Workers;

internal sealed class FaithVerseAutoPostWorker : PeriodicAutoPostWorker
{
    private bool _warnedMissingCreds;

    public FaithVerseAutoPostWorker(ILogger<FaithVerseAutoPostWorker> logger)
        : base(logger, "faithverse", TimeSpan.FromHours(1)) { }

    // Hold posting until the morning; PostIfNeededAsync's own per-day check makes
    // it safe to keep polling hourly after that.
    protected override bool ShouldTickNow() =>
        TimeOnly.FromDateTime(DateTime.Now) >= new TimeOnly(7, 0);

    protected override async Task TickAsync(CancellationToken ct)
    {
        var result = await FaithVerseAutoPost.PostIfNeededAsync(ct);
        switch (result.Outcome)
        {
            case FaithVerseOutcome.Posted:
                Logger.LogInformation("FaithVerse: posted {Reference}", result.Reference);
                TrackTickOutcome(true);
                break;
            case FaithVerseOutcome.AlreadyPosted:
                Logger.LogInformation("FaithVerse: already posted today, skipping");
                TrackTickOutcome(true);
                break;
            case FaithVerseOutcome.MissingCredentials:
                if (!_warnedMissingCreds)
                {
                    Logger.LogWarning("FaithVerse: no Bluesky credentials — widget disabled until configured");
                    _warnedMissingCreds = true;
                }
                break;
            case FaithVerseOutcome.Failed:
                Logger.LogError("FaithVerse: post failed — {Error}", result.ErrorMessage);
                TrackTickOutcome(false, $"{result.Reference ?? "verse lookup"} — {result.ErrorMessage}");
                break;
        }
    }
}
