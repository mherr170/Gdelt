namespace GdeltSearchUI;

// Thin facade over SecretStore — keeps call sites in the UI identical while
// backing storage switches from Windows Credential Manager (per-user) to
// DPAPI LocalMachine (machine-wide, accessible by both UI and Windows service).
internal static class CredentialManager
{
    // ── Bluesky (main search account) ────────────────────────────────────────

    public static void Save(string handle, string password) =>
        SecretStore.Save("Bluesky", handle, password);

    public static (string Handle, string Password)? Load() =>
        SecretStore.Load("Bluesky") is { } t ? (t.Username, t.Password) : null;

    public static void Delete() => SecretStore.Delete("Bluesky");

    // ── Gas Price Bluesky ─────────────────────────────────────────────────────

    public static void SaveGasPriceBluesky(string handle, string password) =>
        SecretStore.Save("GasPriceBluesky", handle, password);

    public static (string Handle, string Password)? LoadGasPriceBluesky() =>
        SecretStore.Load("GasPriceBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Quake Bluesky ─────────────────────────────────────────────────────────

    public static void SaveQuakeBluesky(string handle, string password) =>
        SecretStore.Save("QuakeBluesky", handle, password);

    public static (string Handle, string Password)? LoadQuakeBluesky() =>
        SecretStore.Load("QuakeBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Debt Bluesky ──────────────────────────────────────────────────────────

    public static void SaveDebtBluesky(string handle, string password) =>
        SecretStore.Save("DebtBluesky", handle, password);

    public static (string Handle, string Password)? LoadDebtBluesky() =>
        SecretStore.Load("DebtBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Commodity Bluesky ─────────────────────────────────────────────────────

    public static void SaveCommodityBluesky(string handle, string password) =>
        SecretStore.Save("CommodityBluesky", handle, password);

    public static (string Handle, string Password)? LoadCommodityBluesky() =>
        SecretStore.Load("CommodityBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Yahoo Finance Bluesky ─────────────────────────────────────────────────

    public static void SaveYahooBluesky(string handle, string password) =>
        SecretStore.Save("YahooBluesky", handle, password);

    public static (string Handle, string Password)? LoadYahooBluesky() =>
        SecretStore.Load("YahooBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Gun Violence Bluesky ─────────────────────────────────────────────────

    public static void SaveGunViolenceBluesky(string handle, string password) =>
        SecretStore.Save("GunViolenceBluesky", handle, password);

    public static (string Handle, string Password)? LoadGunViolenceBluesky() =>
        SecretStore.Load("GunViolenceBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── EIA ───────────────────────────────────────────────────────────────────

    public static void SaveEiaApiKey(string apiKey) =>
        SecretStore.Save("EIA", "eia", apiKey);

    public static string? LoadEiaApiKey() =>
        SecretStore.Load("EIA")?.Password;

    // ── API-Ninjas ────────────────────────────────────────────────────────────

    public static void SaveApiNinjasKey(string apiKey) =>
        SecretStore.Save("ApiNinjas", "apininjas", apiKey);

    public static string? LoadApiNinjasKey() =>
        SecretStore.Load("ApiNinjas")?.Password;

    // ── Congress Bluesky ──────────────────────────────────────────────────────

    public static void SaveCongressBluesky(string handle, string password) =>
        SecretStore.Save("CongressBluesky", handle, password);

    public static (string Handle, string Password)? LoadCongressBluesky() =>
        SecretStore.Load("CongressBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── ProPublica API Key ─────────────────────────────────────────────────────

    public static void SaveProPublicaKey(string apiKey) =>
        SecretStore.Save("ProPublica", "propublica", apiKey);

    public static string? LoadProPublicaKey() =>
        SecretStore.Load("ProPublica")?.Password;

    // ── APOD Bluesky ──────────────────────────────────────────────────────────

    public static void SaveApodBluesky(string handle, string password) =>
        SecretStore.Save("ApodBluesky", handle, password);

    public static (string Handle, string Password)? LoadApodBluesky() =>
        SecretStore.Load("ApodBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── NASA API Key ──────────────────────────────────────────────────────────

    public static void SaveNasaApiKey(string apiKey) =>
        SecretStore.Save("NasaApiKey", "nasa", apiKey);

    public static string? LoadNasaApiKey() =>
        SecretStore.Load("NasaApiKey")?.Password;

    // ── Stock Market Bluesky ──────────────────────────────────────────────────

    public static void SaveStockBluesky(string handle, string password) =>
        SecretStore.Save("StockBluesky", handle, password);

    public static (string Handle, string Password)? LoadStockBluesky() =>
        SecretStore.Load("StockBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Weather Alert Bluesky ─────────────────────────────────────────────────

    public static void SaveWeatherBluesky(string handle, string password) =>
        SecretStore.Save("WeatherBluesky", handle, password);

    public static (string Handle, string Password)? LoadWeatherBluesky() =>
        SecretStore.Load("WeatherBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Streaming Bluesky ─────────────────────────────────────────────────────

    public static void SaveStreamingBluesky(string handle, string password) =>
        SecretStore.Save("StreamingBluesky", handle, password);

    public static (string Handle, string Password)? LoadStreamingBluesky() =>
        SecretStore.Load("StreamingBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── Backyard Birds of NJ Bluesky ──────────────────────────────────────────

    public static void SaveBirdBluesky(string handle, string password) =>
        SecretStore.Save("BirdBluesky", handle, password);

    public static (string Handle, string Password)? LoadBirdBluesky() =>
        SecretStore.Load("BirdBluesky") is { } t ? (t.Username, t.Password) : null;

    // ── YouTube Data API Key ──────────────────────────────────────────────────

    public static void SaveYouTubeApiKey(string apiKey) =>
        SecretStore.Save("YouTubeApiKey", "youtube", apiKey);

    public static string? LoadYouTubeApiKey() =>
        SecretStore.Load("YouTubeApiKey")?.Password;

    // ── All bot accounts (for growth worker) ─────────────────────────────────
    public static IReadOnlyList<(string Label, string Slug, string Handle, string Password)> LoadAllBlueskyAccounts()
    {
        var list = new List<(string, string, string, string)>();
        TryAdd(list, "Gas Prices",   "gasprices",   LoadGasPriceBluesky());
        TryAdd(list, "Quake",        "quake",        LoadQuakeBluesky());
        TryAdd(list, "Debt",         "debt",         LoadDebtBluesky());
        TryAdd(list, "Commodity",    "commodity",    LoadCommodityBluesky());
        TryAdd(list, "Yahoo",        "yahoo",        LoadYahooBluesky());
        TryAdd(list, "Gun Violence", "gunviolence",  LoadGunViolenceBluesky());
        TryAdd(list, "Congress",     "congress",     LoadCongressBluesky());
        TryAdd(list, "APOD",         "apod",         LoadApodBluesky());
        TryAdd(list, "Stock",        "stock",        LoadStockBluesky());
        TryAdd(list, "Weather",      "weather",      LoadWeatherBluesky());
        TryAdd(list, "Streaming",    "streaming",    LoadStreamingBluesky());
        TryAdd(list, "NJ Birds",     "njbirds",      LoadBirdBluesky());
        return list;

        static void TryAdd(List<(string, string, string, string)> l, string label, string slug,
            (string Handle, string Password)? creds)
        {
            if (creds is { } c) l.Add((label, slug, c.Handle, c.Password));
        }
    }
}
