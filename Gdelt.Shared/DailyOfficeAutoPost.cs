using System.Globalization;
using System.Text;

namespace GdeltSearchUI;

internal enum DailyOfficeOutcome { Posted, AlreadyPosted, MissingCredentials, Failed }

internal sealed record DailyOfficeResult(
    DailyOfficeOutcome Outcome,
    Office             Office,
    string?            Summary      = null,
    string?            ErrorMessage = null);

// "The Daily Office" — Morning Prayer and Evening Prayer, posted twice a day as
// a short self-reply thread: an opening card (versicle + the day's appointed
// psalms + the two lessons) followed by the collect. Psalms follow the
// traditional 30-day cycle; the lessons are a continuous chapter-by-chapter
// crawl through the OT and NT. No network I/O — every text is embedded.
internal static class DailyOfficeAutoPost
{
    private const string W       = "dailyoffice";
    private const int    MaxPost = 290;

    public static string SlotKey(DateTime now) => now.ToString("yyyy-MM-dd");

    public static async Task<DailyOfficeResult> PostIfNeededAsync(Office office, CancellationToken ct = default)
    {
        var now  = DateTime.Now;
        var slot = SlotKey(now);
        var p    = DailyOfficeProgressStore.Load();

        var lastSlot = office == Office.Morning ? p.LastMorningSlot : p.LastEveningSlot;
        if (lastSlot == slot)
        {
            PostLogger.Info(W, $"{office} Prayer already posted for {slot} — skipping");
            return new(DailyOfficeOutcome.AlreadyPosted, office);
        }

        var creds = CredentialManager.LoadDailyOfficeBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(DailyOfficeOutcome.MissingCredentials, office);
        }

        var otRef   = CurrentRef(DailyOfficeData.OldTestament, p.OtBook, p.OtChapter);
        var ntRef   = CurrentRef(DailyOfficeData.NewTestament, p.NtBook, p.NtChapter);
        var parts   = BuildParts(office, now, otRef, ntRef);
        var summary = $"{office}: Ps {DailyOfficeData.PsalmsFor(office, now)} · {otRef} · {ntRef}";

        PostLogger.Info(W, $"Posting {summary} as {parts.Count}-post thread");

        using var poster = new BlueskyPoster();
        var (ok, err) = await poster.PostThreadAsync(creds.Value.Handle, creds.Value.Password, parts, ct);
        if (!ok)
        {
            PostLogger.Error(W, $"Post failed: {err}");
            return new(DailyOfficeOutcome.Failed, office, summary, err);
        }

        // Advance both courses one chapter — once per office, so a normal day
        // moves each course forward by two chapters.
        (p.OtBook, p.OtChapter) = Next(DailyOfficeData.OldTestament, p.OtBook, p.OtChapter);
        (p.NtBook, p.NtChapter) = Next(DailyOfficeData.NewTestament, p.NtBook, p.NtChapter);
        if (office == Office.Morning) p.LastMorningSlot = slot; else p.LastEveningSlot = slot;
        DailyOfficeProgressStore.Save(p);

        PostLogger.Success(W, $"Posted {summary}");
        return new(DailyOfficeOutcome.Posted, office, summary);
    }

    private static string CurrentRef(IReadOnlyList<BibleBook> books, int bookIdx, int chapter)
    {
        var b = books[bookIdx % books.Count];
        return b.Chapters <= 1 ? b.Name : $"{b.Name} {chapter}";
    }

    private static (int Book, int Chapter) Next(IReadOnlyList<BibleBook> books, int bookIdx, int chapter)
    {
        var b     = books[bookIdx % books.Count];
        var maxCh = Math.Max(1, b.Chapters);
        return chapter >= maxCh
            ? ((bookIdx + 1) % books.Count, 1)
            : (bookIdx, chapter + 1);
    }

    internal static List<string> BuildParts(Office office, DateTime date, string otRef, string ntRef)
    {
        var heading = office == Office.Morning ? "☀️ Morning Prayer" : "🌙 Evening Prayer";

        var card = new StringBuilder();
        card.Append(heading).Append(" — ")
            .Append(date.ToString("dddd, MMMM d", CultureInfo.InvariantCulture)).Append("\n\n");
        card.Append(DailyOfficeData.OpeningVersicle(office)).Append("\n\n");
        card.Append("Psalms ").Append(DailyOfficeData.PsalmsFor(office, date)).Append('\n');
        card.Append("First Lesson: ").Append(otRef).Append('\n');
        card.Append("Second Lesson: ").Append(ntRef);
        card.Append(office == Office.Morning
            ? "\n\n#DailyOffice #MorningPrayer #Scripture #Prayer"
            : "\n\n#DailyOffice #EveningPrayer #Scripture #Prayer");

        var label = office == Office.Evening
            ? "The Collect for Aid Against All Perils"
            : "The Collect";
        var collectBlock = $"{label}\n\n{DailyOfficeData.CollectFor(office, date)}";

        var parts = new List<string> { card.ToString() };
        parts.AddRange(WordWrapToPosts(collectBlock, MaxPost));
        return parts;
    }

    // Splits a block across as many posts as needed (a collect is usually one,
    // occasionally two). Breaks on spaces; "…" bridges the parts.
    private static List<string> WordWrapToPosts(string text, int max)
    {
        if (Graphemes(text) <= max) return [text];

        var posts = new List<string>();
        var cur   = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = cur.Length == 0 ? word : $"{cur} {word}";
            if (cur.Length > 0 && Graphemes(candidate) + 2 > max)
            {
                posts.Add(cur.ToString());
                cur.Clear();
                cur.Append(word);
            }
            else
            {
                cur.Clear();
                cur.Append(candidate);
            }
        }
        if (cur.Length > 0) posts.Add(cur.ToString());

        for (var i = 0; i < posts.Count; i++)
        {
            if (i > 0)               posts[i] = "… " + posts[i];
            if (i < posts.Count - 1) posts[i] = posts[i] + " …";
        }
        return posts;
    }

    private static int Graphemes(string s) => new StringInfo(s).LengthInTextElements;
}
