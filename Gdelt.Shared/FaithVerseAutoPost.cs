namespace GdeltSearchUI;

internal enum FaithVerseOutcome { Posted, AlreadyPosted, MissingCredentials, Failed }

internal sealed record FaithVerseResult(
    FaithVerseOutcome Outcome,
    string?           Reference    = null,
    string?           ErrorMessage = null);

// Daily Bible Verse — posts one curated passage per calendar day to the Faith
// network's verse account. Verse text comes from bible-api.com (World English
// Bible, public domain). On any lookup or post failure nothing is marked, so the
// hourly worker simply retries later the same day.
internal static class FaithVerseAutoPost
{
    private const string W        = "faithverse";
    private const int    BodyCap  = 240; // leave room for reference + hashtags within Bluesky's 300

    public static async Task<FaithVerseResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        var dateKey = DateTime.Now.ToString("yyyy-MM-dd");

        if (FaithVersePostTracker.HasBeenPosted(dateKey))
        {
            PostLogger.Info(W, $"Already posted for {dateKey} — skipping");
            return new(FaithVerseOutcome.AlreadyPosted);
        }

        var creds = CredentialManager.LoadFaithVerseBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(FaithVerseOutcome.MissingCredentials);
        }

        var refs      = FaithVerseData.References;
        var index     = FaithVersePostTracker.PostedCount % refs.Length;
        var reference = refs[index];
        PostLogger.Info(W, $"Verse {index + 1}/{refs.Length}: {reference}");

        BibleVerse? verse;
        using (var client = new BibleApiClient())
            verse = await client.GetVerseAsync(reference, ct);

        if (verse is null)
        {
            PostLogger.Error(W, $"Lookup failed for {reference} — will retry on next tick");
            return new(FaithVerseOutcome.Failed, reference, "bible-api.com lookup failed");
        }

        var text = BuildPostText(verse);

        using var poster = new BlueskyPoster();
        var (ok, error) = await poster.PostTextAsync(creds.Value.Handle, creds.Value.Password, text, ct);

        if (ok)
        {
            FaithVersePostTracker.MarkPosted(dateKey);
            PostLogger.Success(W, $"Posted: {verse.Reference}");
            return new(FaithVerseOutcome.Posted, verse.Reference);
        }

        PostLogger.Error(W, $"Post failed: {error}");
        return new(FaithVerseOutcome.Failed, verse.Reference, error);
    }

    internal static string BuildPostText(BibleVerse verse)
    {
        var body = verse.Text.Length > BodyCap
            ? verse.Text[..(BodyCap - 1)].TrimEnd() + "…"
            : verse.Text;

        var tags = BlueskyPostHelper.HashtagLine(["Bible", "Scripture", "DailyVerse"]);
        return $"“{body}”\n\n— {verse.Reference} ({verse.Translation}){tags}";
    }
}
