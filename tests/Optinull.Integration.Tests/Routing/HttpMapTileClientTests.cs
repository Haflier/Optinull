using System.Net;
using Optinull.Infrastructure.Routing;

namespace Optinull.Integration.Tests.Routing;

public sealed class HttpMapTileClientTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            return respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) };

    private static (HttpMapTileClient Client, StubHandler Handler) Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null,
        MapTileOptions? options = null)
    {
        var handler = new StubHandler(respond ?? ((_, _) => Task.FromResult(Ok())));

        return (new HttpMapTileClient(new HttpClient(handler), options), handler);
    }

    [Fact]
    public async Task TheTemplate_IsFilledWithZoomColumnAndRow()
    {
        var (client, handler) = Create(options: new MapTileOptions
        {
            UrlTemplate = "https://tiles.test/{z}/{x}/{y}.png?key=abc"
        });

        var bytes = await client.GetTileAsync(13, 4400, 2687);

        Assert.Equal(Png, bytes);
        Assert.Equal("https://tiles.test/13/4400/2687.png?key=abc", handler.Requests.Single().ToString());
    }

    [Fact]
    public async Task ThePublicOpenStreetMapServer_IsTheDefault()
    {
        var (client, handler) = Create();

        await client.GetTileAsync(1, 0, 1);

        Assert.Equal("https://tile.openstreetmap.org/1/0/1.png", handler.Requests.Single().ToString());
        Assert.Contains("OpenStreetMap", client.Attribution);
    }

    [Fact]
    public async Task ARepeatedTile_IsServedFromTheCache()
    {
        var (client, handler) = Create();

        await client.GetTileAsync(10, 1, 1);
        var again = await client.GetTileAsync(10, 1, 1);

        Assert.Equal(Png, again);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task TheCache_IsEmptiedWhenItIsFull()
    {
        var (client, handler) = Create(options: new MapTileOptions { CacheEntries = 2 });

        await client.GetTileAsync(10, 1, 1);
        await client.GetTileAsync(10, 2, 1);
        await client.GetTileAsync(10, 3, 1);
        await client.GetTileAsync(10, 1, 1);

        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task AServerError_GivesNoTile_AndIsNotRemembered()
    {
        var calls = 0;

        var (client, handler) = Create((_, _) =>
            Task.FromResult(++calls == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Ok()));

        Assert.Null(await client.GetTileAsync(10, 1, 1));
        Assert.Equal(Png, await client.GetTileAsync(10, 1, 1));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ANetworkFailure_GivesNoTile()
    {
        var (client, _) = Create((_, _) => throw new HttpRequestException("down"));

        Assert.Null(await client.GetTileAsync(10, 1, 1));
    }

    [Fact]
    public async Task ASlowServer_GivesNoTile_AfterTheTimeout()
    {
        var (client, _) = Create(
            async (_, token) =>
            {
                await Task.Delay(Timeout.Infinite, token);

                return Ok();
            },
            new MapTileOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        Assert.Null(await client.GetTileAsync(10, 1, 1));
    }

    [Fact]
    public async Task WhenTheCallerCancels_TheCancellationIsNotSwallowed()
    {
        var (client, _) = Create(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);

            return Ok();
        });

        using var cancel = new CancellationTokenSource();

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetTileAsync(10, 1, 1, cancel.Token));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(23, 0, 0)]
    [InlineData(3, 8, 0)]
    [InlineData(3, 0, 8)]
    [InlineData(3, -1, 0)]
    [InlineData(3, 0, -1)]
    public async Task ATileOutsideTheWorld_IsRefusedWithoutARequest(int zoom, int x, int y)
    {
        var (client, handler) = Create();

        Assert.Null(await client.GetTileAsync(zoom, x, y));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AnEmptyTemplate_TurnsTheBackgroundOff()
    {
        var (client, handler) = Create(options: new MapTileOptions { UrlTemplate = "" });

        Assert.Null(await client.GetTileAsync(10, 1, 1));
        Assert.Empty(handler.Requests);
    }
}
