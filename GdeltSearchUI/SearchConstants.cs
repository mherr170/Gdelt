namespace GdeltSearchUI;

internal static class SearchConstants
{
    internal const int   AutoRefreshMs      = 15 * 60 * 1000;
    internal const int   RateLimitBackoffMs = 30 * 60 * 1000;
    internal const int   PreviewMaxRecords  = 10;
    internal const float ToneNegThreshold   = -3f;
    internal const float TonePosThreshold   = 3f;
}
