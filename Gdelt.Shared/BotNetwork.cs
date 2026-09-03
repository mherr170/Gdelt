namespace GdeltSearchUI;

// One bot account in a network: how to load its Bluesky credentials plus the
// display metadata the cross-promo tooling (starter pack + pinned intro post)
// needs. Slug is the stable identity used by trackers and follower logs — it
// must never change once an account is live, or that state orphans.
public sealed record BotAccount(
    string Slug,
    string Label,
    Func<(string Handle, string Password)?> LoadCreds,
    string IntroBlurb);

// A named group of bot accounts that share a Bluesky starter pack and
// cross-promote each other with a one-time pinned intro post. Each network is
// independent — adding accounts to one, or adding a whole new network, does not
// touch the others.
public sealed record BotNetwork(
    string Slug,
    string Name,
    string Description,
    IReadOnlyList<BotAccount> Accounts,
    bool GrowthEnabled = true)
{
    // Promo line appended to each account's pinned intro post. "{0}" is that
    // account's own copy of the network starter-pack URL.
    public string IntroPromoLine => $"Meet the rest of the {Name} network: {{0}}";
}

public static class BotNetworks
{
    // Real-time data bots. This roster is authoritative for the "Live Wire"
    // starter pack and the pinned intro posts.
    public static readonly BotNetwork LiveWire = new(
        Slug:        "livewire",
        Name:        "Live Wire",
        Description:  "Real-time bots tracking gas prices, quakes, gun violence, backyard birds, energy futures, and daily space photos — straight from the data.",
        Accounts:
        [
            new("gasprices",   "Gas Prices",   CredentialManager.LoadGasPriceBluesky,
                "Automated alerts for US gas prices, updated from EIA data."),
            new("debt",        "Debt",         CredentialManager.LoadDebtBluesky,
                "Daily automated updates on the US national debt, sourced from the Treasury Fiscal Data API."),
            new("yahoo",       "Energy $",     CredentialManager.LoadYahooBluesky,
                "Automated snapshots of energy futures — crude oil, natural gas, gasoline, and heating oil — from Yahoo Finance."),
            new("njbirds",     "NJ Birds",     CredentialManager.LoadBirdBluesky,
                "Automated highlights from the Backyard Birds of New Jersey YouTube channel."),
            new("quake",       "Quakes",       CredentialManager.LoadQuakeBluesky,
                "Automated alerts for significant earthquakes worldwide, sourced from USGS."),
            new("gunviolence", "Gun Violence", CredentialManager.LoadGunViolenceBluesky,
                "Automated tracking of US gun violence news from GDELT, filtered and LLM-verified before posting."),
            new("apod",        "APOD",         CredentialManager.LoadApodBluesky,
                "NASA's Astronomy Picture of the Day, posted automatically every day."),
        ]);

    // Religion-themed daily bots. Placeholder name/description — rename once the
    // full handle set is decided.
    public static readonly BotNetwork Faith = new(
        Slug:        "faith",
        Name:        "Faith Network",
        Description:  "Daily scripture, prayer, and devotional bots — one post a day, straight from the source text.",
        Accounts:
        [
            new("verse", "Daily Bible Verse", CredentialManager.LoadFaithVerseBluesky,
                "A verse of scripture every morning, from the public-domain World English Bible."),
            new("bibleinorder", "The Bible, In Order", CredentialManager.LoadBibleInOrderBluesky,
                "The entire Bible, one verse every hour, Genesis to Revelation — World English Bible (public domain)."),
            new("dailyoffice", "The Daily Office", CredentialManager.LoadDailyOfficeBluesky,
                "Morning & Evening Prayer twice daily — the appointed psalms, two lessons, and a collect, from the Book of Common Prayer (public domain)."),
        ]);

    // Every network. Add new networks here.
    public static IReadOnlyList<BotNetwork> All => [LiveWire, Faith];

    // Accounts that get the daily follow/like growth treatment but belong to no
    // network's cross-promo (no starter pack, no pinned intro). Slugs here must
    // match the values previously used by CredentialManager.LoadAllBlueskyAccounts
    // so follow-trackers and follower logs stay attached.
    public static readonly IReadOnlyList<BotAccount> Ungrouped =
    [
        new("commodity",     "Commodity",       CredentialManager.LoadCommodityBluesky, ""),
        new("congress",      "Congress",        CredentialManager.LoadCongressBluesky,  ""),
        new("stock",         "Stock",           CredentialManager.LoadStockBluesky,     ""),
        new("weather",       "Weather",         CredentialManager.LoadWeatherBluesky,   ""),
        new("streaming",     "Streaming",       CredentialManager.LoadStreamingBluesky, ""),
        new("pigsgonnablow", "Pigs Gonna Blow", CredentialManager.LoadPigsBluesky,      ""),
    ];

    // Accounts eligible for the daily growth run: every account in a
    // growth-enabled network, plus the ungrouped accounts — de-duped by slug,
    // limited to those that actually have credentials configured.
    public static IReadOnlyList<(string Label, string Slug, string Handle, string Password)> GrowthRoster()
    {
        var seen = new HashSet<string>();
        var list = new List<(string, string, string, string)>();

        foreach (var acct in All.Where(n => n.GrowthEnabled).SelectMany(n => n.Accounts).Concat(Ungrouped))
        {
            if (!seen.Add(acct.Slug)) continue;
            if (acct.LoadCreds() is { } c) list.Add((acct.Label, acct.Slug, c.Handle, c.Password));
        }
        return list;
    }
}
