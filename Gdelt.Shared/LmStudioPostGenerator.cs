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
        "TAGS: <one contextual tag>\n\n" +
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
        "TAGS: Inflation\n\n" +
        "Tag rules: PascalCase for multi-word tags; pick ONE tag that adds context beyond gas/fuel — " +
        "good choices: Inflation, Diesel, USEconomy, EnergyPrices, CrudeOil; " +
        "never use GasPrices, Gas, News, Update, Weekly, Data (those are added automatically).";

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
        "TAGS: <tag>\n\n" +
        "CRITICAL: Only mention direction for futures with notable moves (>1%). " +
        "If you mention crude oil, say whether it is WTI, Brent, or both. " +
        "Stay factual — never editorialize about politics or causes. " +
        "If all moved less than 0.5%, say prices were little changed. " +
        "Prices are ~15 min delayed NYMEX futures, not spot prices.\n\n" +
        "Example input: Brent Crude $82.10 (+1.2%), WTI Crude $78.45 (+1.1%), Natural Gas $2.31 (-3.4%), RBOB Gasoline $2.51 (+0.9%), Heating Oil $2.84 (+0.8%)\n" +
        "Example output:\n" +
        "HEADLINE: Crude futures climb over 1% as natural gas slides sharply\n" +
        "TAGS: NatGas\n\n" +
        "Tag rules: pick EXACTLY ONE tag — the commodity that moved most. " +
        "Choose from: Brent, WTI, NatGas, RBOB, HeatingOil, CrudeOil. " +
        "If crude oil broadly moved most, prefer Brent or WTI over CrudeOil. " +
        "Never output more than one tag. Never use generic words like Oil, Energy, Markets, News, Update, Data.";

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

    private const string GunViolenceClassifierPrompt =
        "You classify gun violence news headlines. " +
        "Reply YES if the headline describes a gun homicide — someone shot and killed — " +
        "that occurred inside the United States. " +
        "Reply NO for anything else: injuries only, no confirmed death, foreign incidents, " +
        "military/war, suicide, accidents, fictional events, or unclear US location. " +
        "Reply with EXACTLY one word: YES or NO.";

    private const string GunViolenceDedupePrompt =
        "You deduplicate news headlines about shooting incidents. " +
        "Two headlines are duplicates when they describe the SAME incident: same location and same approximate victim count. " +
        "Given a numbered list of headlines, return ONLY the line numbers to KEEP — " +
        "one per unique incident, choosing the most descriptive headline from each duplicate group. " +
        "Output one integer per line, nothing else. " +
        "If every headline describes a different incident, return all numbers.";

    private const string GunViolenceRecentDuplicatePrompt =
        "You check whether a NEW shooting headline describes the SAME incident as any headline " +
        "already posted (same location and same approximate victim count), even if worded very " +
        "differently by a different news outlet. Reply YES if it matches any already-posted headline, " +
        "otherwise reply NO. Reply with EXACTLY one word: YES or NO.";

    private static readonly Regex _nonAlpha = new(@"[^a-zA-Z0-9]", RegexOptions.Compiled);

    public static async Task<bool> IsDuplicateOfRecentPostsAsync(
        string candidateTitle, IReadOnlyList<string> recentPostedTitles, CancellationToken ct = default)
    {
        if (recentPostedTitles.Count == 0) return false;

        var userMessage = $"NEW: {candidateTitle}\n\nALREADY POSTED:\n" +
            string.Join("\n", recentPostedTitles.Select((t, i) => $"{i + 1}. {t}"));

        try
        {
            var response = await LmStudioClient.CallAsync(GunViolenceRecentDuplicatePrompt, userMessage, 8, 0.0, ct);
            return response.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // LLM unavailable — fall back to the lexical filter only
            return false;
        }
    }

    public static async Task<List<GdeltArticle>> DeduplicateBySameEventAsync(List<GdeltArticle> articles, CancellationToken ct = default)
    {
        if (articles.Count <= 1) return articles;

        var numbered    = articles.Select((a, i) => $"{i + 1}. {a.Title}");
        var userMessage = string.Join("\n", numbered);

        try
        {
            var response = await LmStudioClient.CallAsync(GunViolenceDedupePrompt, userMessage, 64, 0.0, ct);
            if (string.IsNullOrWhiteSpace(response)) return articles;

            var kept = response
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => int.TryParse(line.Trim(), out var n) ? n : -1)
                .Where(n => n >= 1 && n <= articles.Count)
                .Distinct()
                .OrderBy(n => n)
                .Select(n => articles[n - 1])
                .ToList();

            return kept.Count > 0 ? kept : articles;
        }
        catch
        {
            return articles; // LLM unavailable — skip dedup, proceed with all candidates
        }
    }

    public static async Task<bool> IsUSGunHomicideAsync(string headline, CancellationToken ct = default)
    {
        try
        {
            var response = await LmStudioClient.CallAsync(GunViolenceClassifierPrompt, headline, 8, 0.0, ct);
            return response.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // LLM unavailable — trust the keyword pre-filters already applied
            return true;
        }
    }

    public static async Task<(string Headline, string[] Tags)> GenerateYahooFuturesPostAsync(
        IReadOnlyList<OilPriceEntry> prices, Dictionary<string, double>? lastPostPrices = null,
        CancellationToken ct = default)
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
            var text = await LmStudioClient.CallAsync(YahooFuturesSystemPrompt, userMessage, 128, 0.4, ct);
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

    public static async Task<(string Headline, string[] Tags)> GenerateCommodityPostAsync(CommodityData data, CancellationToken ct = default)
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
            var text = await LmStudioClient.CallAsync(CommoditySystemPrompt, userMessage, 128, 0.4, ct);
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

    public static async Task<(string Headline, string Body, string[] Tags)> GenerateQuakePostAsync(QuakeEvent quake, CancellationToken ct = default)
    {
        var fallbackTags     = new[] { "Earthquake", quake.Place.Split(',').Last().Trim().Replace(" ", ""), "Seismic" };
        var fallbackHeadline = $"M {quake.Magnitude:F1} earthquake — {quake.Place}";
        const string fallbackBody = "";

        var tsunami     = quake.TsunamiWarning ? ", tsunami warning issued" : ", no tsunami warning";
        var depth       = quake.DepthKm.HasValue ? $"depth {quake.DepthKm.Value:F1} km" : "depth unknown";
        var userMessage = $"M {quake.Magnitude:F1}, {quake.Place}, {depth}{tsunami}";

        try
        {
            var text = await LmStudioClient.CallAsync(QuakeSystemPrompt, userMessage, 200, 0.4, ct);
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

    public static async Task<(string Headline, string[] Tags)> GenerateGasPricePostAsync(NationalGasPrices prices, CancellationToken ct = default)
    {
        var fallbackTags = new[] { "GasPrices", "Gas", "Inflation" };
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
            var text = await LmStudioClient.CallAsync(GasPriceSystemPrompt, userMessage, 128, 0.4, ct);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback gas price caption.");
                return (fallbackHeadline, fallbackTags);
            }

            AppLogger.Log($"LM Studio gas price response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            var contextTag = tags.FirstOrDefault(t => t.Length > 0);
            var finalTags  = contextTag is not null
                ? new[] { "GasPrices", "Gas", contextTag }
                : new[] { "GasPrices", "Gas" };
            return (headline, finalTags);
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

    public static async Task<(string Headline, string[] Tags)> GenerateDebtPostAsync(NationalDebt debt, CancellationToken ct = default)
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
            var text = await LmStudioClient.CallAsync(DebtSystemPrompt, userMessage, 128, 0.4, ct);
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

    private const string StockSystemPrompt =
        "You write short, punchy Bluesky captions about the US stock market daily close. " +
        "Given index closing values and day-over-day % changes, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one engaging sentence about the market session, max 130 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>\n\n" +
        "CRITICAL: Reflect the actual direction accurately. " +
        "If S&P 500 and Nasdaq both fell, do not say 'stocks climb'. " +
        "If moves are small (<0.3%), say markets were little changed. " +
        "Lead with the most notable move or theme (tech rally, broad selloff, mixed session, etc.).\n\n" +
        "Example input: S&P 500 5,234 (+0.58%), Dow Jones 39,512 (+0.31%), Nasdaq 16,340 (+0.82%), Russell 2K 2,089 (-0.12%)\n" +
        "Example output:\n" +
        "HEADLINE: Tech leads Wall Street higher as S&P 500 and Nasdaq both close in the green\n" +
        "TAGS: SP500, Nasdaq\n\n" +
        "Tag rules: pick EXACTLY TWO tags — good choices: SP500, Nasdaq, DowJones, Russell2000, WallStreet, " +
        "TechStocks, MarketRally, MarketSelloff, BullMarket, BearMarket; " +
        "PascalCase; never use generic words like Stocks, Market, News, Update, Today, Data.";

    private const string WeatherSystemPrompt =
        "You write short, urgent Bluesky posts about severe weather alerts from the National Weather Service. " +
        "Given the alert type, affected area, and NWS headline, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one urgent, plain-language sentence describing the threat, max 130 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>\n\n" +
        "CRITICAL: Convey urgency without being alarmist. Be specific about location when possible. " +
        "Write for people who need to take action, not just observe. " +
        "Do not restate the alert type if it's already in the emoji prefix.\n\n" +
        "Example input: Event: Tornado Warning | Area: Dallas County, TX | Headline: Tornado Warning issued for Dallas County until 7:15 PM CDT\n" +
        "Example output:\n" +
        "HEADLINE: Take shelter immediately — a tornado warning is in effect for Dallas County, TX until 7:15 PM\n" +
        "TAGS: TornadoWarning, Texas\n\n" +
        "Tag rules: first tag = the specific alert type (TornadoWarning, HurricaneWarning, BlizzardWarning, etc.); " +
        "second tag = US state abbreviation or region (Texas, Florida, Midwest, etc.); " +
        "PascalCase; never use generic words like Weather, Alert, Warning, NWS, NOAA, Storm.";

    public static async Task<(string Headline, string[] Tags)> GenerateStockPostAsync(
        IReadOnlyList<StockEntry> entries, CancellationToken ct = default)
    {
        var fallbackTags     = new[] { "StockMarket", "WallStreet" };
        var fallbackHeadline = $"US stock market close — {DateTime.Today:yyyy-MM-dd}";

        if (entries.Count == 0) return (fallbackHeadline, fallbackTags);

        var parts       = entries.Select(e => $"{e.DisplayName} {FmtPrice(e)} ({e.ChangePercent:+0.00;-0.00}%)");
        var userMessage = string.Join(", ", parts);

        try
        {
            var text = await LmStudioClient.CallAsync(StockSystemPrompt, userMessage, 128, 0.4, ct);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback stock caption.");
                return (fallbackHeadline, fallbackTags);
            }
            AppLogger.Log($"LM Studio stock response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback stock caption.");
            return (fallbackHeadline, fallbackTags);
        }

        static string FmtPrice(StockEntry e) =>
            e.Symbol == "^DJI" ? $"{e.Price:N0}" : $"{e.Price:N2}";
    }

    public static async Task<(string Headline, string[] Tags)> GenerateWeatherAlertPostAsync(WeatherAlert alert, CancellationToken ct = default)
    {
        var fallbackTags     = new[] { EventTag(alert.Event), "WeatherAlert" };
        var fallbackHeadline = TrimTo(alert.Headline.Length > 0 ? alert.Headline : alert.Event, 130);

        var instructionPart = !string.IsNullOrWhiteSpace(alert.Instruction)
            ? $" | Instruction: {TrimTo(alert.Instruction.Trim(), 100)}"
            : "";
        var userMessage =
            $"Event: {alert.Event} | Area: {TrimTo(alert.AreaDesc, 80)} | Headline: {TrimTo(alert.Headline, 150)}{instructionPart}";

        try
        {
            var text = await LmStudioClient.CallAsync(WeatherSystemPrompt, userMessage, 128, 0.3, ct);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback weather caption.");
                return (fallbackHeadline, fallbackTags);
            }
            AppLogger.Log($"LM Studio weather response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback weather caption.");
            return (fallbackHeadline, fallbackTags);
        }

        static string EventTag(string evt) =>
            System.Text.RegularExpressions.Regex.Replace(evt.Trim(), @"\s+", "");

        static string TrimTo(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    private const string ApodSystemPrompt =
        "You generate hashtags for NASA's Astronomy Picture of the Day posts on Bluesky. " +
        "Given the image title and its scientific explanation, reply with EXACTLY this one line and nothing else:\n\n" +
        "TAGS: <tag1>, <tag2>, <tag3>, <tag4>\n\n" +
        "Tag rules: pick EXACTLY FOUR tags that best describe the subject and help people discover the post. " +
        "Mix broad discovery tags with specific subject tags. " +
        "Good choices: Space, Astrophotography, Galaxy, Nebula, BlackHole, StarFormation, Supernova, Aurora, " +
        "SolarSystem, Mars, Moon, Jupiter, Saturn, Sun, Hubble, Webb, Telescope, Eclipse, Comet, Cosmos, " +
        "DeepSpace, MilkyWay, Exoplanet, NightSky, Spaceflight, ISS, Rocket; " +
        "PascalCase for multi-word tags; never repeat NASA, APOD, or Astronomy (already included).\n\n" +
        "Example input: Title: Pillars of Creation | Explanation: These towering columns of gas and dust in the Eagle Nebula are 5 light-years tall and are sites of active star formation...\n" +
        "Example output:\n" +
        "TAGS: Nebula, StarFormation, Hubble, DeepSpace";

    public static async Task<(string Headline, string[] Tags)> GenerateApodPostAsync(ApodEntry entry, CancellationToken ct = default)
    {
        var fallbackTags     = new[] { "Astrophotography", "Space" };
        var fallbackHeadline = entry.Title;

        var explanation  = entry.Explanation.Length > 500
            ? entry.Explanation[..497] + "…"
            : entry.Explanation;
        var userMessage  = $"Title: {entry.Title} | Explanation: {explanation}";

        try
        {
            var text = await LmStudioClient.CallAsync(ApodSystemPrompt, userMessage, 160, 0.5, ct);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback APOD caption.");
                return (fallbackHeadline, fallbackTags);
            }
            AppLogger.Log($"LM Studio APOD response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback APOD caption.");
            return (fallbackHeadline, fallbackTags);
        }
    }

    private const string BirdSystemPrompt =
        "You generate hashtags for Backyard Birds of New Jersey YouTube posts on Bluesky. " +
        "Given a bird video title, reply with EXACTLY this one line and nothing else:\n\n" +
        "TAGS: <tag1>, <tag2>, <tag3>\n\n" +
        "Tag rules: pick EXACTLY THREE tags. " +
        "Always include at least one broad discovery tag: Birding, BirdWatching, or BackyardBirds. " +
        "Include the specific bird species as a tag when identifiable (e.g. Cardinal, BlueBird, Woodpecker, Hummingbird, Sparrow, Finch, Robin, Warbler, Hawk). " +
        "Include NewJersey as a tag when it adds context. " +
        "PascalCase for multi-word tags; never use generic words like Bird, Birds, Video, YouTube, Watch, Nature, Wildlife, NJ.";

    public static async Task<string[]> GenerateBirdPostAsync(YouTubeVideo video, CancellationToken ct = default)
    {
        var fallbackTags = new[] { "Birding", "BackyardBirds", "NewJersey" };

        try
        {
            var text = await LmStudioClient.CallAsync(BirdSystemPrompt, video.Title, 64, 0.5, ct);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback bird tags.");
                return fallbackTags;
            }
            AppLogger.Log($"LM Studio bird response: {text.Replace('\n', '|')}");
            var (_, _, tags) = Parse(text, video.Title);
            return tags.Length > 0 ? tags : fallbackTags;
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback bird tags.");
            return fallbackTags;
        }
    }

    private const string CongressSystemPrompt =
        "You write short, factual Bluesky posts about US Congressional votes. " +
        "Given a vote description, chamber, result, and party breakdown, reply with EXACTLY these two lines and nothing else:\n\n" +
        "HEADLINE: <one neutral sentence summarising what was voted on and what happened, max 130 chars, no emojis>\n" +
        "TAGS: <tag1>, <tag2>\n\n" +
        "CRITICAL: stay factual and neutral. Name the chamber (Senate or House). " +
        "Mention the result (passed, failed, confirmed, rejected). " +
        "If there is a strong party-line split, you may note bipartisan or party-line in the headline. " +
        "Do not editorialize or take sides.\n\n" +
        "Example input: Senate | Passage: SAVE Act (H.R. 22) | Passed 67-32 | Dem ✓12 ✗38 | Rep ✓55 ✗0\n" +
        "Example output:\n" +
        "HEADLINE: Senate passes SAVE Act 67-32 in mostly party-line vote\n" +
        "TAGS: Senate, VoterID\n\n" +
        "Tag rules: include the chamber as the first tag; pick ONE topic tag — good choices: " +
        "Senate, House, Nominations, Budget, Healthcare, Immigration, Defense, Infrastructure, Bipartisan; " +
        "PascalCase for multi-word tags; never use generic words like Vote, Bill, Congress, News, Update, Law.";

    public static async Task<(string Headline, string[] Tags)> GenerateCongressPostAsync(CongressVote vote, CancellationToken ct = default)
    {
        var chamber    = vote.Chamber.Equals("Senate", StringComparison.OrdinalIgnoreCase) ? "Senate" : "House";
        var fallbackTags = new[] { chamber, "CongressVotes" };
        var fallbackHeadline = $"{chamber} roll call {vote.RollCall}: {TrimTo(vote.DisplayBill, 100)}";

        var userMessage =
            $"{chamber} | {vote.VoteType}: {vote.DisplayBill} | " +
            $"{vote.Result} {vote.Yes}-{vote.No} | " +
            $"Dem ✓{vote.DemYes} ✗{vote.DemNo} | Rep ✓{vote.RepYes} ✗{vote.RepNo}";

        try
        {
            var text = await LmStudioClient.CallAsync(CongressSystemPrompt, userMessage, 128, 0.3, ct);
            if (string.IsNullOrEmpty(text))
            {
                AppLogger.Log("LM Studio error — using fallback Congress caption.");
                return (fallbackHeadline, fallbackTags);
            }
            AppLogger.Log($"LM Studio Congress response: {text.Replace('\n', '|')}");
            var (headline, _, tags) = Parse(text, fallbackHeadline);
            return (headline, tags.Length > 0 ? tags : fallbackTags);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"LM Studio unavailable ({ex.Message}) — using fallback Congress caption.");
            return (fallbackHeadline, fallbackTags);
        }

        static string TrimTo(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }

    public static async Task<(string Headline, string[] Tags)> GenerateAsync(string rawTitle, CancellationToken ct = default)
    {
        try
        {
            var text = await LmStudioClient.CallAsync(SystemPrompt, rawTitle, 128, 0.2, ct);
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

    private static readonly Regex _thinkBlock = new(@"(<think>[\s\S]*?</think>|<\|channel>thought[\s\S]*?<channel\|>)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex _labelPrefix = new(@"^\*{0,2}(HEADLINE|BODY|TAGS)\*{0,2}:\*{0,2}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static (string Headline, string Body, string[] Tags) Parse(string text, string rawTitle)
    {
        // Strip Gemma-style thinking blocks before parsing
        text = _thinkBlock.Replace(text, "").Trim();

        string headline = rawTitle;
        string body = "";
        string[] tags = [];

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var m = _labelPrefix.Match(line);
            if (!m.Success) continue;

            var label   = m.Groups[1].Value.ToUpperInvariant();
            var content = line[m.Length..].Trim();

            switch (label)
            {
                case "HEADLINE" when content.Length >= 5: headline = content; break;
                case "BODY"     when content.Length > 0:  body     = content; break;
                case "TAGS":                              tags     = ParseTags(content); break;
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
            .Take(4)
            .ToArray();
}
