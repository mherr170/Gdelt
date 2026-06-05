using Gdelt.Service.Workers;
using GdeltSearchUI;

var runOnce        = args.Contains("--run-once");
var createPackMode = args.Contains("--create-starter-pack");

// One-shot mode: create the Bluesky starter pack then exit.
if (createPackMode)
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    await BlueskyStarterPackCreator.CreateAsync(cts.Token);
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
    options.ServiceName = "Gdelt Auto Post");

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

var host = builder.Build();

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
