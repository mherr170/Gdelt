using System.Text.RegularExpressions;

namespace GdeltSearchUI;

internal static class LmStudioPostGenerator
{
    private const string SystemPrompt =
        "You prepare news headlines for Bluesky posts. " +
        "Given a raw headline, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <clean concise headline, no source attribution, max 180 chars>\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "Example input: Investigation underway into deadly shooting in east - side San Antonio , police say\n" +
        "Example output:\n" +
        "HEADLINE: Deadly shooting investigation underway in east San Antonio\n" +
        "TAGS: SanAntonioShooting, GunViolence, Texas\n\n" +
        "Hashtag rules: acronyms stay uppercase (NATO, FBI, CIA, UN, WHO); " +
        "multi-word concepts use PascalCase (MassShooting, NuclearDeal); " +
        "never use generic words like News, Breaking, Today, Story, Investigation, Underway.";

    private const string GasPriceSystemPrompt =
        "You write short, punchy Bluesky captions about US gas prices. " +
        "Given weekly national average pump prices, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one engaging sentence about the prices, max 120 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "Example input: Regular $3.456, Mid-Grade $3.789, Premium $4.012, Diesel $4.234 — week of 2026-04-21\n" +
        "Example output:\n" +
        "HEADLINE: National gas prices hold near $3.45 as diesel edges higher this week\n" +
        "TAGS: GasPrices, FuelCosts, USEconomy\n\n" +
        "Tag rules: PascalCase for multi-word tags; keep tags relevant to gas, fuel, or the economy; " +
        "never use generic words like News, Update, Weekly, Data.";

    private const string QuakeSystemPrompt =
        "You write informative Bluesky posts about earthquakes for a general audience. " +
        "Given earthquake data, reply with EXACTLY these three lines and nothing else:\n\n" +
        "HEADLINE: <punchy headline, max 120 chars>\n" +
        "BODY: <2-3 sentences of plain-language context, max 220 chars. Cover: what this magnitude means for people, whether the depth amplifies or dampens shaking, and tsunami status. No jargon.>\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "Magnitude guide: M2-3 minor/rarely felt; M4 light/some damage; M5 moderate/damage possible; " +
        "M6 strong/serious damage; M7 major/widespread damage; M8+ great/catastrophic.\n" +
        "Depth guide: <70 km shallow (amplifies surface shaking); 70-300 km intermediate; >300 km deep (less surface impact).\n\n" +
        "Example input: M 6.2, 45 km SSW of Tokyo, Japan, depth 35 km, no tsunami warning\n" +
        "Example output:\n" +
        "HEADLINE: Magnitude 6.2 earthquake strikes near Tokyo\n" +
        "BODY: A strong M6.2 quake hit just 45 km from Tokyo. At only 35 km depth — shallow — shaking is amplified at the surface. No tsunami warning has been issued.\n" +
        "TAGS: Earthquake, Japan, Tokyo\n\n" +
        "Tag rules: always include Earthquake or Quake; include the region/country; " +
        "PascalCase for multi-word tags; never use generic words like Event, Update, News.";

    private static readonly Regex _nonAlpha = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

    public static async Task<(string Headline, string Body, string[] Tags)> GenerateQuakePostAsync(QuakeEvent quake)
    {
        var fallbackTags     = new[] { "Earthquake", quake.Place.Split(',').Last().Trim().Replace(" ", ""), "Seismic" };
        var fallbackHeadline = $"M {quake.Magnitude:F1} earthquake — {quake.Place}";
        const string fallbackBody = "";

        var tsunami     = quake.TsunamiWarning ? ", tsunami warning issued" : ", no tsunami warning";
        var depth       = quake.DepthKm.HasValue ? $"depth {quake.DepthKm.Value:F1} km" : "depth unknown";
        var userMessage = $"M {quake.Magnitude:F1}, {quake.Place}, {depth}{tsunami}";

        try
        {
            var text = await LmStudioClient.CallAsync(QuakeSystemPrompt, userMessage, 200, 0.4);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback quake caption.");
                return (fallbackHeadline, fallbackBody, fallbackTags);
            }

            AppLogger.Log($"LM Studio quake response: {text.Replace('\n', '|')}");
            var (headline, body, tags) = Parse(text, fallbackHeadline);
            return (headline, body, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback quake caption.");
            return (fallbackHeadline, fallbackBody, fallbackTags);
        }
    }

    public static async Task<(string Headline, string[] Tags)> GenerateGasPricePostAsync(NationalGasPrices prices)
    {
        var fallbackTags = new[] { "GasPrices", "FuelCosts", "USEconomy" };
        var fallbackHeadline = $"US national average gas prices for the week of {prices.Period}";

        var userMessage =
            $"Regular {Fmt(prices.Regular)}, Mid-Grade {Fmt(prices.MidGrade)}, " +
            $"Premium {Fmt(prices.Premium)}, Diesel {Fmt(prices.Diesel)} — week of {prices.Period}";

        try
        {
            var text = await LmStudioClient.CallAsync(GasPriceSystemPrompt, userMessage, 128, 0.4);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback gas price caption.");
                return (fallbackHeadline, fallbackTags);
            }

            AppLogger.Log($"LM Studio gas price response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback gas price caption.");
            return (fallbackHeadline, fallbackTags);
        }

        static string Fmt(double? v) => v.HasValue ? $"${v.Value:F3}" : "N/A";
    }

    public static async Task<(string Headline, string[] Tags)> GenerateAsync(string rawTitle)
    {
        try
        {
            var text = await LmStudioClient.CallAsync(SystemPrompt, rawTitle, 128, 0.2);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using raw title + rule-based tags.");
                return (rawTitle, HashtagGenerator.Generate(rawTitle));
            }

            AppLogger.Log($"LM Studio response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, rawTitle);
            return (headline, tags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using raw title + rule-based tags.");
            return (rawTitle, HashtagGenerator.Generate(rawTitle));
        }
    }

    private static (string Headline, string Body, string[] Tags) Parse(string text, string rawTitle)
    {
        string headline = rawTitle;
        string body = "";
        string[] tags = [];

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("HEADLINE:", StringComparison.OrdinalIgnoreCase))
            {
                var h = line["HEADLINE:".Length..].Trim();
                if (h.Length >= 5) headline = h;
            }
            else if (line.StartsWith("BODY:", StringComparison.OrdinalIgnoreCase))
            {
                body = line["BODY:".Length..].Trim();
            }
            else if (line.StartsWith("TAGS:", StringComparison.OrdinalIgnoreCase))
            {
                tags = ParseTags(line["TAGS:".Length..]);
            }
        }

        // Fallback: model used pipe-separated format "Headline text|#tag1 #tag2 #tag3"
        if (headline == rawTitle && text.Contains('|'))
        {
            var parts = text.Split('|', 2);
            var h = parts[0].Trim();
            if (h.Length >= 5) headline = h;
            tags = ParseTags(parts[1]);
        }

        if (tags.Length == 0)
            tags = HashtagGenerator.Generate(headline);

        return (headline, body, tags);
    }

    private static string[] ParseTags(string segment) =>
        segment.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => _nonAlpha.Replace(t.TrimStart('#'), ""))
            .Where(t => t.Length >= 2 && char.IsLetter(t[0]))
            .Take(3)
            .ToArray();
}
