namespace GdeltSearchUI;

internal enum CongressAutoPostOutcome { Posted, NoNewVotes, MissingCredentials, MissingApiKey, Failed }

internal sealed record CongressAutoPostResult(
    CongressAutoPostOutcome Outcome,
    string?                 PostedKey    = null,
    string?                 ErrorMessage = null);

internal static class CongressAutoPost
{
    private const string W = "congress";

    private static readonly string[] SkipKeywords =
        ["quorum", "adjourn", "journal", "speaker", "motion to table",
         "unanimous consent", "recess", "sine die", "prayer", "pledge"];

    public static async Task<CongressAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        var apiKey = CredentialManager.LoadProPublicaKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            PostLogger.Warn(W, "No ProPublica API key configured");
            return new(CongressAutoPostOutcome.MissingApiKey);
        }

        var creds = CredentialManager.LoadCongressBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, "No Bluesky credentials configured");
            return new(CongressAutoPostOutcome.MissingCredentials);
        }

        var congress = CongressApiClient.CurrentCongress(DateTime.Now);
        List<CongressVote> votes;

        try
        {
            using var client = new CongressApiClient(apiKey);
            var houseTask  = client.GetRecentHouseVotesAsync(congress, ct);
            var senateTask = client.GetRecentSenateVotesAsync(congress, ct);
            await Task.WhenAll(houseTask, senateTask);
            votes = houseTask.Result.Concat(senateTask.Result)
                .OrderByDescending(v => v.VoteTime)
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"Fetch failed: {ex.Message}");
            return new(CongressAutoPostOutcome.Failed, ErrorMessage: ex.Message);
        }

        PostLogger.Info(W, $"Fetched {votes.Count} votes from the {congress}th Congress");

        var candidate = votes
            .Where(IsInteresting)
            .Where(v => !CongressPostTracker.HasBeenPosted(v.UniqueKey))
            .OrderByDescending(Importance)
            .FirstOrDefault();

        if (candidate is null)
        {
            PostLogger.Info(W, "No new interesting votes to post");
            return new(CongressAutoPostOutcome.NoNewVotes);
        }

        PostLogger.Info(W,
            $"Posting: [{candidate.Chamber}] Roll #{candidate.RollCall} — {candidate.DisplayBill} → {candidate.Result}");

        string headline; string[] tags;
        try   { (headline, tags) = await LmStudioPostGenerator.GenerateCongressPostAsync(candidate); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            headline = TrimTo(candidate.DisplayBill, 100);
            tags     = ["CongressVotes"];
            PostLogger.Warn(W, $"Caption generation failed ({ex.Message}), using fallback");
        }

        var text = BuildPostText(candidate, headline, tags);

        using var poster = new BlueskyPoster();
        var (ok, error) = await poster.PostTextAsync(creds.Value.Handle, creds.Value.Password, text, ct);

        if (ok)
        {
            CongressPostTracker.MarkPosted(candidate.UniqueKey);
            PostLogger.Success(W, $"Posted {candidate.UniqueKey} | {candidate.Result}");
            return new(CongressAutoPostOutcome.Posted, candidate.UniqueKey);
        }

        PostLogger.Error(W, $"Post failed: {error}");
        return new(CongressAutoPostOutcome.Failed, ErrorMessage: error);
    }

    internal static string BuildPostText(CongressVote v, string headline, string[] tags)
    {
        var chamber     = v.Chamber.Equals("Senate", StringComparison.OrdinalIgnoreCase) ? "Senate" : "House";
        var dateStr     = v.VoteTime != DateTime.MinValue ? v.VoteTime.ToString("MMM d, yyyy") : "";
        var tallyLine   = $"Result: {v.Result} — {v.Yes} YES / {v.No} NO";
        var partyLine   = $"Dem ✓{v.DemYes} ✗{v.DemNo} | Rep ✓{v.RepYes} ✗{v.RepNo}";

        var allTags     = tags.Prepend(chamber).Prepend("CongressVotes").Distinct().ToArray();
        var hashtagLine = BlueskyPostHelper.HashtagLine(allTags);

        return
            $"🏛️ {chamber} — {dateStr}\n" +
            $"{TrimTo(v.DisplayBill, 120)}\n\n" +
            $"{tallyLine}\n" +
            $"{partyLine}\n\n" +
            $"{headline}" +
            $"{hashtagLine}";
    }

    private static bool IsInteresting(CongressVote v)
    {
        var combined = $"{v.Question} {v.Description}";
        return !SkipKeywords.Any(kw => combined.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    private static double Importance(CongressVote v)
    {
        double score = 0;

        // Reward high partisan splits
        var demTotal = v.DemYes + v.DemNo;
        var repTotal = v.RepYes + v.RepNo;
        if (demTotal > 0 && repTotal > 0)
        {
            var demYesPct = (double)v.DemYes / demTotal;
            var repYesPct = (double)v.RepYes / repTotal;
            score += Math.Abs(demYesPct - repYesPct) * 50;
        }

        // Reward close votes
        var totalVotes = v.Yes + v.No;
        if (totalVotes > 0)
        {
            var margin = Math.Abs((double)(v.Yes - v.No)) / totalVotes;
            score += (1.0 - margin) * 30;
        }

        // Reward substantive vote types
        var desc = $"{v.Question} {v.VoteType} {v.Description}";
        if (desc.Contains("passage",    StringComparison.OrdinalIgnoreCase)) score += 20;
        if (desc.Contains("cloture",    StringComparison.OrdinalIgnoreCase)) score += 15;
        if (desc.Contains("override",   StringComparison.OrdinalIgnoreCase)) score += 25;
        if (desc.Contains("nomination", StringComparison.OrdinalIgnoreCase)) score += 10;
        if (desc.Contains("resolution", StringComparison.OrdinalIgnoreCase)) score += 5;

        // Recency bonus
        var ageH = (DateTime.Now - v.VoteTime).TotalHours;
        if      (ageH < 3)  score += 10;
        else if (ageH < 24) score += 5;

        return score;
    }

    private static string TrimTo(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
