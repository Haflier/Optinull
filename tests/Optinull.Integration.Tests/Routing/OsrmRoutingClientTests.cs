using System.Net;
using System.Text;
using Optinull.Infrastructure.Routing;

namespace Optinull.Integration.Tests.Routing;

public sealed class OsrmRoutingClientTests
{
    private static readonly GeoPoint Depot = new(52.5, 13.4);
    private static readonly GeoPoint[] Customers = [new(52.51, 13.41), new(52.52, 13.42)];

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

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static (OsrmRoutingClient Client, StubHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK, OsrmOptions? options = null)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(Json(body, status)));

        return (new OsrmRoutingClient(new HttpClient(handler), options), handler);
    }

    private const string AsymmetricTable =
        """{"code":"Ok","distances":[[0,100,200],[110,0,50],[210,60,0]]}""";

    // ---- Table API ---------------------------------------------------------------------------

    [Fact]
    public async Task Table_IsParsedAsADirectedMatrix()
    {
        var (client, handler) = Create(AsymmetricTable);

        var matrix = await client.GetCostMatrixAsync(Depot, Customers);

        Assert.Equal(2, matrix.CustomerCount);
        Assert.Equal(100, matrix.FromDepot(0));
        Assert.Equal(110, matrix.ToDepot(0));
        Assert.Equal(50, matrix.Between(0, 1));
        Assert.Equal(60, matrix.Between(1, 0));

        var url = handler.Requests.Single().ToString();
        Assert.Contains("/table/v1/driving/13.4,52.5;13.41,52.51;13.42,52.52", url);
        Assert.Contains("annotations=distance", url);
    }

    [Fact]
    public async Task Table_NullDistance_IsReportedAsUnreachable_NotAsZero()
    {
        var (client, _) = Create("""{"code":"Ok","distances":[[0,100,200],[110,0,null],[210,60,0]]}""");

        var error = await Assert.ThrowsAsync<UnreachableRouteException>(() => client.GetCostMatrixAsync(Depot, Customers));

        Assert.Equal(1, error.From);
        Assert.Equal(2, error.To);
    }

    [Fact]
    public async Task Table_HttpError_SaysWhatTheServiceSaid()
    {
        var (client, _) = Create("""{"code":"InvalidQuery","message":"bad coordinates"}""", HttpStatusCode.BadRequest);

        var error = await Assert.ThrowsAsync<RoutingException>(() => client.GetCostMatrixAsync(Depot, Customers));

        Assert.Contains("InvalidQuery", error.Message);
        Assert.Contains("400", error.Message);
    }

    [Fact]
    public async Task Table_NonOkCode_IsAnError()
    {
        var (client, _) = Create("""{"code":"NoTable","message":"nope"}""");

        var error = await Assert.ThrowsAsync<RoutingException>(() => client.GetCostMatrixAsync(Depot, Customers));

        Assert.Contains("NoTable", error.Message);
    }

    [Fact]
    public async Task Table_WrongSize_IsAnError()
    {
        var (client, _) = Create("""{"code":"Ok","distances":[[0,1],[1,0]]}""");

        await Assert.ThrowsAsync<RoutingException>(() => client.GetCostMatrixAsync(Depot, Customers));
    }

    [Fact]
    public async Task Table_NotJson_IsAnError()
    {
        var (client, _) = Create("<html>oops</html>");

        await Assert.ThrowsAsync<RoutingException>(() => client.GetCostMatrixAsync(Depot, Customers));
    }

    [Fact]
    public async Task TooManyLocations_AreRefusedBeforeAnyRequest()
    {
        var (client, handler) = Create(AsymmetricTable, options: new OsrmOptions { MaxLocations = 2 });

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetCostMatrixAsync(Depot, Customers));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task InvalidCoordinates_AreRefusedBeforeAnyRequest()
    {
        var (client, handler) = Create(AsymmetricTable);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetCostMatrixAsync(new GeoPoint(91, 0), Customers));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetCostMatrixAsync(Depot, [new GeoPoint(0, double.NaN)]));
        await Assert.ThrowsAsync<ArgumentException>(() => client.GetCostMatrixAsync(Depot, []));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SlowService_TimesOut_WithARoutingException()
    {
        var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });

        var client = new OsrmRoutingClient(new HttpClient(handler), new OsrmOptions { Timeout = TimeSpan.FromMilliseconds(50) });

        var error = await Assert.ThrowsAsync<RoutingException>(() => client.GetCostMatrixAsync(Depot, Customers));

        Assert.Contains("timed out", error.Message);
    }

    [Fact]
    public async Task CancelledCaller_GetsACancellation_NotARoutingException()
    {
        var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return Json("{}");
        });

        var client = new OsrmRoutingClient(new HttpClient(handler));
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetCostMatrixAsync(Depot, Customers, source.Token));
    }

    [Fact]
    public async Task SameLocations_AreAskedForOnlyOnce()
    {
        var (client, handler) = Create(AsymmetricTable);

        var first = await client.GetCostMatrixAsync(Depot, Customers);
        var second = await client.GetCostMatrixAsync(Depot, Customers);

        Assert.Same(first, second);
        Assert.Single(handler.Requests);
    }

    // ---- Route API ---------------------------------------------------------------------------

    [Fact]
    public async Task Route_ReturnsTheGeometryInTravelOrder()
    {
        var (client, handler) = Create(
            """
            {"code":"Ok","routes":[{"distance":1234.5,"duration":99,
              "geometry":{"type":"LineString","coordinates":[[13.4,52.5],[13.405,52.505],[13.41,52.51]]}}]}
            """);

        var route = await client.GetRouteAsync([Depot, Customers[0]]);

        Assert.Equal(3, route.Points.Count);
        Assert.Equal(new GeoPoint(52.5, 13.4), route.Points[0]);
        Assert.Equal(new GeoPoint(52.51, 13.41), route.Points[^1]);
        Assert.Equal(1234.5, route.DistanceMeters);
        Assert.Equal(99, route.DurationSeconds);

        var url = handler.Requests.Single().ToString();
        Assert.Contains("/route/v1/driving/13.4,52.5;13.41,52.51", url);
        Assert.Contains("geometries=geojson", url);
    }

    [Fact]
    public async Task Route_NeedsTwoStops_AndAMalformedAnswerIsAnError()
    {
        var (client, _) = Create("""{"code":"Ok","routes":[]}""");

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetRouteAsync([Depot]));
        await Assert.ThrowsAsync<RoutingException>(() => client.GetRouteAsync([Depot, Customers[0]]));
    }

    [Fact]
    public async Task Route_NoRoute_IsAnError()
    {
        var (client, _) = Create("""{"code":"NoRoute","message":"Impossible route"}""");

        var error = await Assert.ThrowsAsync<RoutingException>(() => client.GetRouteAsync([Depot, Customers[0]]));

        Assert.Contains("NoRoute", error.Message);
    }

    // ---- Live service (optional) -------------------------------------------------------------

    // Set OPTINULL_LIVE_OSRM=1 to call the public demo server. Without it this test returns at once.
    [Fact]
    public async Task Live_PublicDemoServer_AnswersForAFewPointsInBerlin()
    {
        if (Environment.GetEnvironmentVariable("OPTINULL_LIVE_OSRM") != "1")
            return;

        var client = new OsrmRoutingClient(new HttpClient());
        var depot = new GeoPoint(52.5200, 13.4050);
        GeoPoint[] customers = [new(52.5163, 13.3777), new(52.5076, 13.3904)];

        var matrix = await client.GetCostMatrixAsync(depot, customers);
        var route = await client.GetRouteAsync([depot, customers[0], customers[1], depot]);

        Assert.True(matrix.FromDepot(0) > 0);
        Assert.True(route.Points.Count > 4);
    }
}
