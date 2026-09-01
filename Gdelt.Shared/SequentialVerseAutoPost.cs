using System.Globalization;
using System.Text;

namespace GdeltSearchUI;

internal enum SequentialVerseOutcome { Posted, AlreadyPostedThisHour, MissingCredentials, Complete, Failed }

internal sealed record SequentialVerseResult(
    SequentialVerseOutcome Outcome,
    string?                Reference    = null,
    int                    Ordinal      = 0,
    string?                ErrorMessage = null);

// "The Bible, In Order" — one verse per hour, Genesis 1:1 straight through to
// Revelation 22:21, then stop. Verse text is the World English Bible via
// bible-api.com; a whole chapter is cached at a time so the hourly tick usually
// touches no network. Verses too long for one Bluesky post go out as a
// self-reply thread with the reference + progress on the final post.
internal static class SequentialVerseAutoPost
{
    private const string W       = "bibleinorder";
    private const int    MaxPost = 300;

    public static string SlotKey(DateTime now) => now.ToString("yyyy-MM-ddTHH");

    public static async Task<SequentialVerseResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        var slot = SlotKey(DateTime.Now);
        var p    = BibleProgressStore.Load();

        if (p.Complete)
            return new(SequentialVerseOutcome.Complete, Ordinal: p.Ordinal);

        if (p.LastSlot == slot)
        {
            PostLogger.Info(W, $"Already posted for hour {slot} — skipping");
            return new(SequentialVerseOutcome.AlreadyPostedThisHour, Ordinal: p.Ordinal);
        }

        var creds = CredentialManager.LoadBibleInOrderBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(SequentialVerseOutcome.MissingCredentials);
        }

        if (p.NextIndex >= p.CachedVerses.Count)
        {
            if (!await AdvanceChapterAsync(p, ct))
            {
                PostLogger.Error(W, "Chapter fetch failed — will retry next hour");
                return new(SequentialVerseOutcome.Failed, ErrorMessage: "bible-api.com chapter fetch failed");
            }
            if (p.Complete)
            {
                BibleProgressStore.Save(p);
                PostLogger.Success(W, $"Reached the end of Revelation — {p.Ordinal:N0} verses posted. Crawl complete.");
                return new(SequentialVerseOutcome.Complete, Ordinal: p.Ordinal);
            }
        }

        var cv           = p.CachedVerses[p.NextIndex];
        var reference    = $"{p.CachedBook} {p.CachedChapter}:{cv.Verse}";
        var ordinalAfter = p.Ordinal + 1;
        var parts        = BuildParts(cv.Text, reference, ordinalAfter);

        PostLogger.Info(W, $"Posting {reference} ({ordinalAfter:N0}/{BibleBooks.TotalVerses:N0})" +
                           (parts.Count > 1 ? $" as {parts.Count}-post thread" : ""));

        using var poster = new BlueskyPoster();
        var (ok, err) = parts.Count == 1
            ? await poster.PostTextAsync(creds.Value.Handle, creds.Value.Password, parts[0], ct)
            : await poster.PostThreadAsync(creds.Value.Handle, creds.Value.Password, parts, ct);

        if (!ok)
        {
            PostLogger.Error(W, $"Post failed: {err}");
            return new(SequentialVerseOutcome.Failed, reference, p.Ordinal, err);
        }

        p.NextIndex++;
        p.Ordinal   = ordinalAfter;
        p.LastSlot  = slot;
        BibleProgressStore.Save(p);

        PostLogger.Success(W, $"Posted {reference} ({ordinalAfter:N0}/{BibleBooks.TotalVerses:N0})");
        return new(SequentialVerseOutcome.Posted, reference, ordinalAfter);
    }

    // Steps the cursor to the next chapter and loads it into the cache. Sets
    // Complete once the last book is passed. Returns false only on a fetch
    // failure (caller retries next hour without advancing).
    private static async Task<bool> AdvanceChapterAsync(BibleReadingProgress p, CancellationToken ct)
    {
        int bi = p.BookIndex;
        int ch = p.Chapter;

        if (p.CachedChapter != 0) // a chapter is cached and now finished — move on
        {
            ch++;
            if (ch > BibleBooks.All[bi].Chapters) { bi++; ch = 1; }
        }

        if (bi >= BibleBooks.All.Count)
        {
            p.Complete = true;
            return true;
        }

        var book = BibleBooks.All[bi];
        using var client = new BibleApiClient();
        var verses = await client.GetChapterAsync(book.Name, ch, book.SingleChapterVerses, ct);
        if (verses is null || verses.Count == 0) return false;

        p.BookIndex     = bi;
        p.Chapter       = ch;
        p.CachedBook    = book.Name;
        p.CachedChapter = ch;
        p.CachedVerses  = verses.Select(v => new CachedVerse(v.Verse, v.Text)).ToList();
        p.NextIndex     = 0;
        return true;
    }

    // Splits a verse into 1..N Bluesky-sized posts. One post when it fits;
    // otherwise the verse text leads and the "— Ref (WEB) / progress" footer
    // lands on the final post, with "…" markers bridging the parts.
    internal static List<string> BuildParts(string verseText, string reference, int ordinalAfter)
    {
        var footer = $"— {reference} (WEB)\n\U0001F4D6 {ordinalAfter:N0} / {BibleBooks.TotalVerses:N0}";
        var full   = $"{verseText}\n\n{footer}";

        if (Graphemes(full) <= MaxPost) return [full];
        if (Graphemes(verseText) <= MaxPost) return [verseText, footer];

        var reserve = Graphemes(footer) + 4; // "… " + "…" + slack
        var budget  = Math.Max(40, MaxPost - reserve);
        var chunks  = WordWrap(verseText, budget);

        var parts = new List<string>(chunks.Count);
        for (int i = 0; i < chunks.Count; i++)
        {
            if (i == 0)                    parts.Add(chunks[i] + " …");
            else if (i < chunks.Count - 1) parts.Add("… " + chunks[i] + " …");
            else                           parts.Add("… " + chunks[i] + "\n\n" + footer);
        }
        return parts;
    }

    private static List<string> WordWrap(string text, int budget)
    {
        var chunks = new List<string>();
        var cur    = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = cur.Length == 0 ? word : $"{cur} {word}";
            if (Graphemes(candidate) > budget && cur.Length > 0)
            {
                chunks.Add(cur.ToString());
                cur.Clear();
                cur.Append(word);
            }
            else
            {
                cur.Clear();
                cur.Append(candidate);
            }
        }
        if (cur.Length > 0) chunks.Add(cur.ToString());
        return chunks.Count > 0 ? chunks : [text];
    }

    private static int Graphemes(string s) => new StringInfo(s).LengthInTextElements;
}
