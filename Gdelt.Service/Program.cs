using Gdelt.Service;
using Gdelt.Service.Workers;
using GdeltSearchUI;

var runOnce        = args.Contains("--run-once");
var createPackMode = args.Contains("--create-starter-pack");
var pinIntroMode   = args.Contains("--pin-intro-posts");
var pinIntroDryRun = args.Contains("--pin-intro-posts-dry-run");
var postBirdNow    = args.Contains("--post-bird-now");
var postApodNow    = args.Contains("--post-apod-now");
var postVerseNow   = args.Contains("--post-verse-now");
var postNextVerse  = args.Contains("--post-next-verse-now");

// One-shot mode: sync the "Live Wire" Bluesky starter pack across the roster, then exit.
if (createPackMode)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await StarterPackSync.SyncAllAsync(Console.WriteLine, cts.Token);
    return;
}

// One-shot mode: post + pin a "Live Wire" intro post on each roster account, then exit.
// Run --create-starter-pack first so each account already has its own pack to link to.
if (pinIntroMode)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await PinnedIntroPostSync.PinAllAsync(Console.WriteLine, cts.Token);
    return;
}

// Read-only preview: auth + look up each account's starter pack link, print the
// exact intro post text that --pin-intro-posts would send — no post, no pin.
if (pinIntroDryRun)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await PinnedIntroPostSync.PinAllAsync(Console.WriteLine, cts.Token, dryRun: true);
    return;
}

// One-shot mode: post today's NASA APOD now, then exit.
if (postApodNow)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var result = await ApodAutoPost.PostIfNeededAsync(cts.Token);
    Console.WriteLine($"APOD one-shot result: {result.Outcome}{(result.ErrorMessage is null ? "" : $" — {result.ErrorMessage}")}");
    return;
}

// One-shot mode: post today's Daily Bible Verse now, then exit.
if (postVerseNow)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var result = await FaithVerseAutoPost.PostIfNeededAsync(cts.Token);
    Console.WriteLine($"Faith verse one-shot: {result.Outcome}{(result.ErrorMessage is null ? "" : $" — {result.ErrorMessage}")}");
    return;
}

// One-shot mode: post the next verse of the sequential Bible crawl now, then exit.
if (postNextVerse)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var result = await SequentialVerseAutoPost.PostIfNeededAsync(cts.Token);
    Console.WriteLine($"Bible-in-order one-shot: {result.Outcome}" +
                      $"{(result.Reference is null ? "" : $" — {result.Reference} ({result.Ordinal}/{BibleBooks.TotalVerses})")}" +
                      $"{(result.ErrorMessage is null ? "" : $" — {result.ErrorMessage}")}");
    return;
}

// One-shot mode: post the NJ Birds top videos now, then exit.
// Picks the most recently elapsed scheduled time so the slot key matches what
// the running service uses, preventing double-posts or silent no-ops.
if (postBirdNow)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var defaults = new[] { new TimeOnly(8, 0), new TimeOnly(18, 0) };
    var now = DateTime.Now;
    var due = defaults.Where(t => now.TimeOfDay >= t.ToTimeSpan())
                      .DefaultIfEmpty(defaults[0])
                      .Last();
    var slot   = BirdAutoPost.SlotKey(now, due);
    var result = await BirdAutoPost.PostIfNeededAsync(slot, cts.Token);
    Console.WriteLine($"Bird one-shot result: {result.Outcome} (posted {result.PostedCount}){(result.ErrorMessage is null ? "" : $" — {result.ErrorMessage}")}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options =>
    options.ServiceName = "Gdelt Auto Post");

// Dashboard listens on loopback only — this is an operator view, not a public API.
builder.WebHost.UseUrls("http://127.0.0.1:5080");

builder.Services.AddHostedService<GasAutoPostWorker>();
builder.Services.AddHostedService<DebtAutoPostWorker>();
builder.Services.AddHostedService<YahooAutoPostWorker>();
builder.Services.AddHostedService<QuakeAutoPostWorker>();
builder.Services.AddHostedService<GunViolenceAutoPostWorker>();
builder.Services.AddHostedService<CongressAutoPostWorker>();
builder.Services.AddHostedService<ApodAutoPostWorker>();
builder.Services.AddHostedService<StockAutoPostWorker>();
builder.Services.AddHostedService<WeatherAutoPostWorker>();
builder.Services.AddHostedService<BlueskyGrowthWorker>();
builder.Services.AddHostedService<BirdAutoPostWorker>();
builder.Services.AddHostedService<FaithVerseAutoPostWorker>();
builder.Services.AddHostedService<SequentialVerseAutoPostWorker>();

var host = builder.Build();

host.UseDefaultFiles();
host.UseStaticFiles();
LiveActivityEndpoints.Map(host);

if (runOnce)
{
    // Run each worker once then exit — useful for testing without installing the service.
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("--run-once mode: running all workers once then exiting.");

    var tasks = host.Services.GetServices<IHostedService>()
        .OfType<BackgroundService>()
        .Select(w => w.StartAsync(cts.Token));
    await Task.WhenAll(tasks);

    logger.LogInformation("--run-once complete.");
}
else
{
    await host.RunAsync();
}
