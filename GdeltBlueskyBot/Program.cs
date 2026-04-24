using GdeltBlueskyBot;
using GdeltBlueskyBot.Services;
using Microsoft.Extensions.Configuration;

var runtimeCredentials = PromptForMissingCredentials();

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
        config.AddUserSecrets<Worker>(optional: true);
        config.AddEnvironmentVariables();

        // Overlay anything the user just typed — highest priority wins.
        if (runtimeCredentials.Count > 0)
            config.AddInMemoryCollection(runtimeCredentials);
    })
    .ConfigureServices((_, services) =>
    {
        services.AddHttpClient<GdeltClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GdeltBlueskyBot/1.0");
        });

        services.AddSingleton<BlueskyService>();
        services.AddSingleton<PostedArticlesRepository>();
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();

// ---------------------------------------------------------------------------
// Prompts for Bluesky handle and App Password when they are absent from every
// other config source (appsettings, user-secrets, environment variables).
// Returns only the keys that were actually collected so the overlay stays minimal.
// ---------------------------------------------------------------------------
static Dictionary<string, string?> PromptForMissingCredentials()
{
    // Build a temporary config to check what's already present.
    var probe = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddUserSecrets<Worker>(optional: true)
        .AddEnvironmentVariables()
        .Build();

    var collected = new Dictionary<string, string?>();

    var handle = probe["Bluesky:Handle"];
    if (string.IsNullOrWhiteSpace(handle) || handle == "yourhandle.bsky.social")
    {
        Console.Write("Bluesky handle (e.g. you.bsky.social): ");
        var input = Console.ReadLine()?.Trim();
        if (!string.IsNullOrWhiteSpace(input))
            collected["Bluesky:Handle"] = input;
    }

    var password = probe["Bluesky:AppPassword"];
    if (string.IsNullOrWhiteSpace(password) || password == "xxxx-xxxx-xxxx-xxxx")
    {
        Console.Write("Bluesky App Password: ");
        var input = ReadPassword();
        if (!string.IsNullOrWhiteSpace(input))
            collected["Bluesky:AppPassword"] = input;
    }

    if (collected.Count > 0)
        Console.WriteLine(); // blank line before host output

    return collected;
}

// Reads a password from stdin without echoing characters.
static string ReadPassword()
{
    var sb = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0)
            {
                sb.Remove(sb.Length - 1, 1);
                Console.Write("\b \b");
            }
            continue;
        }
        sb.Append(key.KeyChar);
        Console.Write('*');
    }
    Console.WriteLine();
    return sb.ToString();
}
