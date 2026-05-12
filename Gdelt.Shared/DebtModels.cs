using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── Treasury Fiscal Data API response shape ───────────────────────────────────

internal sealed record DebtApiResponse
{
    [JsonPropertyName("data")]
    public List<DebtApiRow> Data { get; init; } = [];
}

internal sealed record DebtApiRow
{
    [JsonPropertyName("record_date")]
    public string RecordDate { get; init; } = "";

    [JsonPropertyName("debt_held_public_amt")]
    public string? DebtHeldPublicAmt { get; init; }

    [JsonPropertyName("intragov_hold_amt")]
    public string? IntragovHoldAmt { get; init; }

    [JsonPropertyName("tot_pub_debt_out_amt")]
    public string? TotalPublicDebtOutstandingAmt { get; init; }
}

// ── Domain model ──────────────────────────────────────────────────────────────

internal sealed record DebtSnapshot
{
    public DateOnly RecordDate         { get; init; }
    public decimal  DebtHeldByPublic   { get; init; }
    public decimal  IntragovHoldings   { get; init; }
    public decimal  TotalPublicDebt    { get; init; }
}

internal sealed record NationalDebt
{
    public DebtSnapshot? Current  { get; init; }
    public DebtSnapshot? Previous { get; init; }

    public string? ErrorMessage { get; init; }
    public bool    IsSuccess => ErrorMessage is null;
    public bool    IsRateLimited { get; init; }
}
