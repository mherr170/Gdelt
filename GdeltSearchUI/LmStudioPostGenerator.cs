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
        "Given weekly national average pump prices AND week-over-week changes, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one engaging sentence about the prices, max 120 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "CRITICAL: The headline MUST reflect the actual direction of change. " +
        "If deltas are negative, prices FELL — never say 'surge', 'rise', 'climb', or 'record high'. " +
        "If deltas are positive, prices ROSE — never say 'fall', 'drop', 'ease', or 'relief'. " +
        "If most deltas are near zero, say prices held steady. " +
        "Lead with the 'Biggest mover' fuel when it's notable. " +
        "If a 'Year-over-year' line is present, weave that long-term context into the headline when it's striking (e.g. 'lowest in a year', 'still up 30 cents from last year').\n\n" +
        "Example input: Regular $3.456 (-0.020), Mid-Grade $3.789 (-0.015), Premium $4.012 (-0.018), Diesel $4.234 (+0.005) — week of 2026-04-21\n" +
        "Biggest mover: Regular (-0.020).\n" +
        "Year-over-year (Regular): -$0.32 vs week of 2025-04-22.\n" +
        "Example output:\n" +
        "HEADLINE: US pump prices ease again — Regular gas now 32 cents cheaper than a year ago\n" +
        "TAGS: GasPrices, FuelCosts, USEconomy\n\n" +
        "Tag rules: PascalCase for multi-word tags; keep tags relevant to gas, fuel, or the economy; " +
        "never use generic words like News, Update, Weekly, Data.";

    private const string DebtSystemPrompt =
        "You write short, factual Bluesky captions about the US national debt. " +
        "Given the current total debt and the day-over-day change, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one neutral sentence, max 120 chars, no emojis, no political framing>\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "CRITICAL: stay neutral and factual. Never editorialize about whether debt is good or bad. " +
        "If the day-over-day change is positive, say debt rose/grew/climbed. " +
        "If negative, say debt fell/declined/decreased. " +
        "If near zero, say it held roughly steady.\n\n" +
        "Example input: Total debt $36.512T, day-over-day +$8.42B (+0.0231%) on 2026-04-25\n" +
        "Example output:\n" +
        "HEADLINE: US national debt rose $8.4B overnight to $36.51 trillion\n" +
        "TAGS: NationalDebt, USTreasury, FiscalPolicy\n\n" +
        "Tag rules: PascalCase for multi-word tags; keep tags relevant to debt, treasury, or fiscal topics; " +
        "never use generic words like News, Update, Daily, Data.";

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

    private const string YahooFuturesSystemPrompt =
        "You write short, punchy Bluesky captions about energy futures prices. " +
        "Given near-real-time NYMEX futures prices and day-over-day changes, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one engaging sentence about the energy futures snapshot, max 120 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>\n\n" +
        "CRITICAL: Only mention direction for futures with notable moves (>1%). " +
        "If you mention crude oil, say whether it is WTI, Brent, or both. " +
        "Stay factual — never editorialize about politics or causes. " +
        "If all moved less than 0.5%, say prices were little changed. " +
        "Prices are ~15 min delayed NYMEX futures, not spot prices.\n\n" +
        "Example input: Brent Crude $82.10 (+1.2%), WTI Crude $78.45 (+1.1%), Natural Gas $2.31 (-3.4%), RBOB Gasoline $2.51 (+0.9%), Heating Oil $2.84 (+0.8%)\n" +
        "Example output:\n" +
        "HEADLINE: Crude futures climb over 1% as natural gas slides sharply\n" +
        "TAGS: Oil, NatGas\n\n" +
        "Tag rules: PascalCase for multi-word tags; prefer short high-traffic tags: Oil, NatGas, EnergyMarkets, Commodities, Brent, WTI, RBOB; " +
        "never use generic words like News, Update, Daily, Data, EnergyFutures, CrudeOil, NaturalGas.";

    private const string CommoditySystemPrompt =
        "You write short, punchy Bluesky captions about energy commodity prices. " +
        "Given current prices and day-over-day % changes, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one engaging sentence about the energy market snapshot, max 120 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "CRITICAL: Only mention direction for commodities with notable moves (>1%). " +
        "If you mention crude oil, say whether it is WTI, Brent, or both. " +
        "Stay factual — never editorialize about politics or causes. " +
        "If all moved less than 0.5%, say prices were little changed.\n\n" +
        "Example input: Brent $82.10 (+1.2%), WTI $78.45 (+1.1%), Natural Gas $2.31 (-3.4%), Heating Oil $2.84 (+0.8%), RBOB Gasoline $2.51 (+0.9%)\n" +
        "Example output:\n" +
        "HEADLINE: Crude oil climbs over 1% as natural gas falls sharply\n" +
        "TAGS: CrudeOil, NaturalGas, EnergyMarkets\n\n" +
        "Tag rules: PascalCase for multi-word tags; keep tags relevant to energy or fuel markets; " +
        "never use generic words like News, Update, Daily, Data.";

    private static readonly Regex _nonAlpha = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

    public static async Task<(string Headline, string[] Tags)> GenerateYahooFuturesPostAsync(
        IReadOnlyList<OilPriceEntry> prices, Dictionary<string, double>? lastPostPrices = null)
    {
        var fallbackTags     = new[] { "EnergyFutures", "CrudeOil", "NaturalGas" };
        var fallbackHeadline = $"Energy futures snapshot — {DateTime.Today:yyyy-MM-dd}";

        if (prices.Count == 0) return (fallbackHeadline, fallbackTags);

        static string Fmt(OilPriceEntry e) => e.Code switch
        {
            "NATURAL_GAS"                    => $"${e.Price:F3}",
            "RBOB_GASOLINE" or "HEATING_OIL" => $"${e.Price:F3}",
            _                                => $"${e.Price:F2}",
        };
        string Delta(OilPriceEntry e)
        {
            double? baseline = lastPostPrices is not null &&
                               lastPostPrices.TryGetValue(e.Code, out var lp) && lp != 0
                ? lp
                : e.Previous;
            if (!baseline.HasValue || baseline.Value == 0) return "";
            var pct = (e.Price - baseline.Value) / baseline.Value * 100.0;
            return $" ({(pct >= 0 ? "+" : "")}{pct:F1}%)";
        }

        var parts       = prices.Select(e => $"{e.DisplayName} {Fmt(e)}{Delta(e)}");
        var userMessage = string.Join(", ", parts);

        try
        {
            var text = await LmStudioClient.CallAsync(YahooFuturesSystemPrompt, userMessage, 128, 0.4);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback Yahoo futures caption.");
                return (fallbackHeadline, fallbackTags);
            }
            AppLogger.Log($"LM Studio Yahoo futures response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback Yahoo futures caption.");
            return (fallbackHeadline, fallbackTags);
        }
    }

    public static async Task<(string Headline, string[] Tags)> GenerateCommodityPostAsync(CommodityData data)
    {
        var fallbackTags = new[] { "CommodityMarkets", "CrudeOil", "GoldPrice" };
        var fallbackHeadline = $"Commodity market snapshot — {DateTime.Today:yyyy-MM-dd}";

        if (data.Prices.Count == 0) return (fallbackHeadline, fallbackTags);

        static string Fmt(CommodityPrice p) => p.Slug switch
        {
            "gold"        => $"${p.Price:N1}",
            "natural_gas" => $"${p.Price:F3}",
            "copper"      => $"${p.Price:F3}",
            _             => $"${p.Price:F2}",
        };
        static string Delta(double curr, double? prev)
        {
            if (!prev.HasValue || prev.Value == 0) return "";
            var pct = (curr - prev.Value) / prev.Value * 100.0;
            return $" ({(pct >= 0 ? "+" : "")}{pct:F1}%)";
        }

        var parts = data.Prices.Select(p => $"{p.DisplayName} {Fmt(p)}{Delta(p.Price, p.Previous)}");
        var userMessage = string.Join(", ", parts);

        try
        {
            var text = await LmStudioClient.CallAsync(CommoditySystemPrompt, userMessage, 128, 0.4);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback commodity caption.");
                return (fallbackHeadline, fallbackTags);
            }
            AppLogger.Log($"LM Studio commodity response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback commodity caption.");
            return (fallbackHeadline, fallbackTags);
        }
    }

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

        var prev = prices.Previous;
        var ya   = prices.YearAgo;

        var movers = new (string Name, double? Curr, double? Prev)[]
        {
            ("Regular",   prices.Regular,  prev?.Regular),
            ("Mid-Grade", prices.MidGrade, prev?.MidGrade),
            ("Premium",   prices.Premium,  prev?.Premium),
            ("Diesel",    prices.Diesel,   prev?.Diesel),
        };
        var biggest = movers
            .Where(m => m.Curr.HasValue && m.Prev.HasValue)
            .OrderByDescending(m => Math.Abs(m.Curr!.Value - m.Prev!.Value))
            .FirstOrDefault();
        var biggestLine = biggest.Name is not null
            ? $"\nBiggest mover: {biggest.Name} ({(biggest.Curr! - biggest.Prev! >= 0 ? "+" : "-")}{Math.Abs(biggest.Curr!.Value - biggest.Prev!.Value):F3})."
            : "";

        var yoyLine = (ya is not null && prices.Regular.HasValue && ya.Regular.HasValue)
            ? $"\nYear-over-year (Regular): {(prices.Regular.Value - ya.Regular.Value >= 0 ? "+" : "-")}${Math.Abs(prices.Regular.Value - ya.Regular.Value):F2} vs week of {ya.Period}."
            : "";

        var userMessage =
            $"Regular {Fmt(prices.Regular)}{Delta(prices.Regular, prev?.Regular)}, " +
            $"Mid-Grade {Fmt(prices.MidGrade)}{Delta(prices.MidGrade, prev?.MidGrade)}, " +
            $"Premium {Fmt(prices.Premium)}{Delta(prices.Premium, prev?.Premium)}, " +
            $"Diesel {Fmt(prices.Diesel)}{Delta(prices.Diesel, prev?.Diesel)} " +
            $"— week of {prices.Period}" +
            (prev is { Period.Length: > 0 } ? $" (vs week of {prev.Period})" : "") +
            biggestLine +
            yoyLine;

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
        static string Delta(double? curr, double? prv)
        {
            if (!curr.HasValue || !prv.HasValue) return "";
            var d = curr.Value - prv.Value;
            return $" ({(d >= 0 ? "+" : "-")}{Math.Abs(d):F3})";
        }
    }

    public static async Task<(string Headline, string[] Tags)> GenerateDebtPostAsync(NationalDebt debt)
    {
        var fallbackTags = new[] { "NationalDebt", "USTreasury", "FiscalPolicy" };
        var date = debt.Current?.RecordDate.ToString("yyyy-MM-dd") ?? "today";
        var fallbackHeadline = $"US national debt update for {date}";

        if (debt.Current is null) return (fallbackHeadline, fallbackTags);

        var totalT = debt.Current.TotalPublicDebt / 1_000_000_000_000m;
        var pct    = DebtApiClient.PercentChange(debt);

        string changeLine;
        if (debt.Previous is not null)
        {
            var diffB = (debt.Current.TotalPublicDebt - debt.Previous.TotalPublicDebt) / 1_000_000_000m;
            var sign  = diffB >= 0 ? "+" : "-";
            var pctStr = pct.HasValue ? $" ({(pct >= 0 ? "+" : "")}{pct:F4}%)" : "";
            changeLine = $", day-over-day {sign}${Math.Abs(diffB):F2}B{pctStr}";
        }
        else
        {
            changeLine = ", day-over-day unavailable";
        }

        var userMessage = $"Total debt ${totalT:F3}T{changeLine} on {date}";

        try
        {
            var text = await LmStudioClient.CallAsync(DebtSystemPrompt, userMessage, 128, 0.4);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback debt caption.");
                return (fallbackHeadline, fallbackTags);
            }

            AppLogger.Log($"LM Studio debt response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback debt caption.");
            return (fallbackHeadline, fallbackTags);
        }
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
