using System.Text.Json;
using GdeltSearchUI;

namespace Gdelt.Service;

// Wires up the live activity dashboard: static files are served from wwwroot,
// this maps the data endpoints they call.
internal static class LiveActivityEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        // Snapshot of recent events, for the page to render immediately on load
        // before the live SSE stream catches up.
        app.MapGet("/api/recent", () => Results.Json(LiveActivityBroadcaster.Recent(), JsonOptions));

        app.MapGet("/events", async (HttpContext ctx) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";

            var (id, reader) = LiveActivityBroadcaster.Subscribe();
            try
            {
                await ctx.Response.WriteAsync(":ok\n\n", ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);

                await foreach (var evt in reader.ReadAllAsync(ctx.RequestAborted))
                {
                    var json = JsonSerializer.Serialize(evt, JsonOptions);
                    await ctx.Response.WriteAsync($"data: {json}\n\n", ctx.RequestAborted);
                    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* client disconnected */ }
            finally
            {
                LiveActivityBroadcaster.Unsubscribe(id);
            }
        });
    }
}
