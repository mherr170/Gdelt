using System.Net;
using System.Text;
using GdeltSearchUI;
using Xunit;

namespace Gdelt.Shared.Tests;

public class ApodApiClientTests
{
    private const string ApiJson = """
        {"date":"2026-09-10","title":"Orion in Red","explanation":"Gas and dust.",
         "url":"https://apod.nasa.gov/apod/image/orion.jpg","media_type":"image"}
        """;

    private const string ApodPageHtml = """
        <div class="media-detail-hero__media foo"><img src="https://science.nasa.gov/orion-page.jpg" /></div>
        </figure><h2 class="title">Orion from the Page</h2>
        <p class="media-detail-hero__description"><strong>Explanation:</strong> From the page.</p>
        """;

    private static readonly TimeSpan[] Delays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)];

    private sealed class FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int ApiCalls  { get; private set; }
        public int PageCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var isApi = request.RequestUri!.Host == "api.nasa.gov";
            var n     = isApi ? ++ApiCalls : ++PageCalls;
            return Task.FromResult(respond(request, n));
        }
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code) { Content = new StringContent("oops") };
    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private static (ApodApiClient Client, List<TimeSpan> Waits) Create(FakeHandler handler)
    {
        var waits = new List<TimeSpan>();
        var client = new ApodApiClient("key", handler, Delays, (d, _) => { waits.Add(d); return Task.CompletedTask; });
        return (client, waits);
    }

    [Fact]
    public async Task Retries_transient_500s_then_uses_api_response()
    {
        var handler = new FakeHandler((req, n) => n < 3 ? Status(HttpStatusCode.InternalServerError) : Json(ApiJson));
        var (client, waits) = Create(handler);

        var entry = await client.GetTodayAsync();

        Assert.Equal("Orion in Red", entry?.Title);
        Assert.Equal(3, handler.ApiCalls);
        Assert.Equal(0, handler.PageCalls);
        Assert.Equal([Delays[0], Delays[1]], waits);
    }

    [Fact]
    public async Task Retries_timeouts_and_429()
    {
        var handler = new FakeHandler((req, n) => n switch
        {
            1 => throw new TaskCanceledException("timeout"),
            2 => Status(HttpStatusCode.TooManyRequests),
            _ => Json(ApiJson),
        });
        var (client, _) = Create(handler);

        var entry = await client.GetTodayAsync();

        Assert.Equal("Orion in Red", entry?.Title);
        Assert.Equal(3, handler.ApiCalls);
    }

    [Fact]
    public async Task Falls_back_to_apod_page_when_api_stays_down()
    {
        var handler = new FakeHandler((req, n) =>
            req.RequestUri!.Host == "api.nasa.gov" ? Status(HttpStatusCode.InternalServerError) : Html(ApodPageHtml));
        var (client, waits) = Create(handler);

        var entry = await client.GetTodayAsync();

        Assert.Equal("Orion from the Page", entry?.Title);
        Assert.Equal("From the page.", entry?.Explanation);
        Assert.Equal(Delays.Length + 1, handler.ApiCalls);
        Assert.Equal(Delays, waits);
        Assert.Equal(1, handler.PageCalls);
    }

    [Fact]
    public async Task Throws_unavailable_when_api_and_page_both_down()
    {
        var handler = new FakeHandler((req, n) => Status(HttpStatusCode.InternalServerError));
        var (client, _) = Create(handler);

        var ex = await Assert.ThrowsAsync<ApodUnavailableException>(() => client.GetTodayAsync());

        Assert.Contains("HTTP 500", ex.Message);
        Assert.Equal(Delays.Length + 1, handler.ApiCalls);
    }

    [Fact]
    public async Task Does_not_retry_client_errors()
    {
        var handler = new FakeHandler((req, n) =>
            req.RequestUri!.Host == "api.nasa.gov" ? Status(HttpStatusCode.Forbidden) : Status(HttpStatusCode.InternalServerError));
        var (client, waits) = Create(handler);

        // A bad key is not an outage: no retries, and a hard failure (null) rather than "unavailable".
        var entry = await client.GetTodayAsync();

        Assert.Null(entry);
        Assert.Equal(1, handler.ApiCalls);
        Assert.Empty(waits);
    }

    [Fact]
    public async Task Caller_cancellation_is_not_retried()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler((req, n) => { cts.Cancel(); throw new TaskCanceledException(); });
        var (client, waits) = Create(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetTodayAsync(cts.Token));

        Assert.Equal(1, handler.ApiCalls);
        Assert.Empty(waits);
    }
}
