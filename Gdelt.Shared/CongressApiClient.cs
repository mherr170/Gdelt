using System.Net.Http.Json;
using System.Text.Json;

namespace GdeltSearchUI;

internal sealed class CongressApiClient : IDisposable
{
    private const string BaseUrl = "https://api.propublica.org/congress/v1/";

    private readonly HttpClient _http;

    public CongressApiClient(string apiKey)
    {
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    public Task<IReadOnlyList<CongressVote>> GetRecentHouseVotesAsync(
        int congress, CancellationToken ct = default) =>
        GetRecentAsync(congress, "house", ct);

    public Task<IReadOnlyList<CongressVote>> GetRecentSenateVotesAsync(
        int congress, CancellationToken ct = default) =>
        GetRecentAsync(congress, "senate", ct);

    private async Task<IReadOnlyList<CongressVote>> GetRecentAsync(
        int congress, string chamber, CancellationToken ct)
    {
        var url      = $"{congress}/{chamber}/votes/recent.json";
        var response = await _http.GetFromJsonAsync<CongressApiResponse>(url, ct);

        if (response?.Results?.Votes is not { } rows || rows.Count == 0)
            return [];

        return rows
            .Select(MapRow)
            .OrderByDescending(v => v.VoteTime)
            .ToList();
    }

    private static CongressVote MapRow(CongressVoteRow r)
    {
        var voteTime = ParseEastern(r.Date, r.Time);

        return new CongressVote
        {
            Chamber     = r.Chamber,
            Congress    = r.Congress,
            Session     = r.Session,
            RollCall    = r.RollCall,
            Question    = r.Question    ?? "",
            Description = r.Description ?? "",
            Result      = r.Result      ?? "",
            VoteType    = r.VoteType    ?? "",
            VoteTime    = voteTime,
            BillNumber  = r.Bill?.Number,
            BillTitle   = r.Bill?.ShortTitle ?? r.Bill?.Title,
            VoteUri     = r.VoteUri,
            Yes         = r.Total?.Yes        ?? 0,
            No          = r.Total?.No         ?? 0,
            NotVoting   = r.Total?.NotVoting  ?? 0,
            DemYes      = r.Democratic?.Yes   ?? 0,
            DemNo       = r.Democratic?.No    ?? 0,
            RepYes      = r.Republican?.Yes   ?? 0,
            RepNo       = r.Republican?.No    ?? 0,
        };
    }

    private static DateTime ParseEastern(string? date, string? time)
    {
        if (string.IsNullOrWhiteSpace(date)) return DateTime.MinValue;

        var raw = string.IsNullOrWhiteSpace(time) ? date : $"{date} {time}";

        if (!DateTime.TryParse(raw, out var dt)) return DateTime.MinValue;

        try
        {
            var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), eastern).ToLocalTime();
        }
        catch
        {
            return dt;
        }
    }

    // 119th Congress began January 2025; each Congress lasts 2 years.
    public static int CurrentCongress(DateTime now) =>
        119 + (now.Year - 2025) / 2;

    public void Dispose() => _http.Dispose();
}
