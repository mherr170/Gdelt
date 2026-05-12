using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── ProPublica API response shape ────────────────────────────────────────────

internal sealed class CongressApiResponse
{
    [JsonPropertyName("status")]  public string          Status  { get; init; } = "";
    [JsonPropertyName("results")] public CongressResults? Results { get; init; }
}

internal sealed class CongressResults
{
    [JsonPropertyName("chamber")]     public string              Chamber { get; init; } = "";
    [JsonPropertyName("num_results")] public int                 NumResults { get; init; }
    [JsonPropertyName("votes")]       public List<CongressVoteRow> Votes { get; init; } = [];
}

internal sealed class CongressVoteRow
{
    [JsonPropertyName("congress")]    public int     Congress  { get; init; }
    [JsonPropertyName("chamber")]     public string  Chamber   { get; init; } = "";
    [JsonPropertyName("session")]     public int     Session   { get; init; }
    [JsonPropertyName("roll_call")]   public int     RollCall  { get; init; }
    [JsonPropertyName("vote_uri")]    public string? VoteUri   { get; init; }
    [JsonPropertyName("date")]        public string? Date      { get; init; }
    [JsonPropertyName("time")]        public string? Time      { get; init; }
    [JsonPropertyName("question")]    public string? Question   { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("result")]      public string? Result    { get; init; }
    [JsonPropertyName("vote_type")]   public string? VoteType  { get; init; }
    [JsonPropertyName("bill")]        public CongressBill?       Bill        { get; init; }
    [JsonPropertyName("total")]       public CongressVoteTotals? Total       { get; init; }
    [JsonPropertyName("democratic")]  public CongressPartyTotals? Democratic  { get; init; }
    [JsonPropertyName("republican")]  public CongressPartyTotals? Republican  { get; init; }
    [JsonPropertyName("independent")] public CongressPartyTotals? Independent { get; init; }
}

internal sealed class CongressBill
{
    [JsonPropertyName("bill_id")]     public string? BillId     { get; init; }
    [JsonPropertyName("number")]      public string? Number     { get; init; }
    [JsonPropertyName("title")]       public string? Title      { get; init; }
    [JsonPropertyName("short_title")] public string? ShortTitle { get; init; }
    [JsonPropertyName("sponsor")]     public string? Sponsor    { get; init; }
    [JsonPropertyName("api_uri")]     public string? ApiUri     { get; init; }
}

internal sealed class CongressVoteTotals
{
    [JsonPropertyName("yes")]        public int Yes       { get; init; }
    [JsonPropertyName("no")]         public int No        { get; init; }
    [JsonPropertyName("present")]    public int Present   { get; init; }
    [JsonPropertyName("not_voting")] public int NotVoting { get; init; }
}

internal sealed class CongressPartyTotals
{
    [JsonPropertyName("yes")] public int Yes { get; init; }
    [JsonPropertyName("no")]  public int No  { get; init; }
}

// ── Domain model ─────────────────────────────────────────────────────────────

internal sealed record CongressVote
{
    public string   Chamber     { get; init; } = "";
    public int      Congress    { get; init; }
    public int      Session     { get; init; }
    public int      RollCall    { get; init; }
    public string   Question    { get; init; } = "";
    public string   Description { get; init; } = "";
    public string   Result      { get; init; } = "";
    public string   VoteType    { get; init; } = "";
    public DateTime VoteTime    { get; init; }
    public string?  BillNumber  { get; init; }
    public string?  BillTitle   { get; init; }
    public int      Yes         { get; init; }
    public int      No          { get; init; }
    public int      NotVoting   { get; init; }
    public int      DemYes      { get; init; }
    public int      DemNo       { get; init; }
    public int      RepYes      { get; init; }
    public int      RepNo       { get; init; }
    public string?  VoteUri     { get; init; }

    public string UniqueKey => $"{Congress}-{Chamber.ToLowerInvariant()}-{Session}-{RollCall}";

    public string DisplayBill =>
        !string.IsNullOrWhiteSpace(BillNumber)
            ? string.IsNullOrWhiteSpace(BillTitle) ? BillNumber : $"{BillNumber} — {BillTitle}"
            : Description;
}
