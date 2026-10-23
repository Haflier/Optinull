using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using Optinull.Reporting.Charts;

namespace Optinull.Integration.Tests.Support;

/// <summary>
/// One stub that plays both the routing server (osrm.test) and the tile server (tiles.test).
/// It computes distances and road shapes from the coordinates in each request, and each part
/// can be switched off or made to hang.
/// </summary>
internal sealed class World : HttpMessageHandler
{
    // Six places about 5 km apart, so the picture needs several tiles. Depot, customers, fleet.
    public const string Places =
        "52.50, 13.40\n" +
        "52.52, 13.37 4\n" +
        "52.54, 13.41 5\n" +
        "52.53, 13.45 3\n" +
        "52.49, 13.44 4\n" +
        "52.48, 13.38 3\n" +
        "52.51, 13.34 4\n" +
        "done\n" +
        "2 14";

    private World(byte[] tile) => Tile = tile;

    public byte[] Tile { get; }

    public bool TilesDown { get; set; }

    /// <summary>Tile requests hang until the caller gives up.</summary>
    public bool TilesBlock { get; set; }

    public bool TableDown { get; set; }

    public bool RouteDown { get; set; }

    public ConcurrentQueue<Uri> TileRequests { get; } = new();

    public ConcurrentQueue<Uri> Tables { get; } = new();

    public ConcurrentQueue<Uri> Routes { get; } = new();

    public static World Create()
    {
        // Any valid PNG will do as a tile.
        var depot = new RoutePoint(13.40, 52.50);

        var tile = new RoadRouteChartRenderer().Render(
            depot,
            [new RoadChartRoute([depot, new(13.41, 52.51)], [new(13.41, 52.51)], "tile")],
            "tile");

        return new World(tile);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;

        if (uri.Host == "tiles.test")
        {
            TileRequests.Enqueue(uri);

            if (TilesBlock)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            return TilesDown
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Tile) };
        }

        // OSRM: /table/v1/driving/lon,lat;lon,lat?... or /route/v1/driving/lon,lat;lon,lat?...
        var parts = uri.AbsolutePath.Trim('/').Split('/');

        var points = parts[3]
            .Split(';')
            .Select(pair => pair.Split(','))
            .Select(pair => (
                Lon: double.Parse(pair[0], CultureInfo.InvariantCulture),
                Lat: double.Parse(pair[1], CultureInfo.InvariantCulture)))
            .ToArray();

        if (parts[0] == "table")
        {
            Tables.Enqueue(uri);

            return TableDown ? Down() : Json(Table(points));
        }

        Routes.Enqueue(uri);

        return RouteDown ? Down() : Json(Route(points));
    }

    private static HttpResponseMessage Down() =>
        new(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("""{"code":"Error","message":"down"}""", Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static double Meters((double Lon, double Lat) a, (double Lon, double Lat) b) =>
        Math.Sqrt(Math.Pow(a.Lat - b.Lat, 2) + Math.Pow(a.Lon - b.Lon, 2)) * 111_000;

    private static string Table((double Lon, double Lat)[] points)
    {
        var rows = points.Select(from => "[" + string.Join(",", points.Select(to =>
            Meters(from, to).ToString("0.##", CultureInfo.InvariantCulture))) + "]");

        return $$"""{"code":"Ok","distances":[{{string.Join(",", rows)}}]}""";
    }

    private static string Route((double Lon, double Lat)[] points)
    {
        var coordinates = string.Join(",", points.Select(point =>
            $"[{point.Lon.ToString(CultureInfo.InvariantCulture)},{point.Lat.ToString(CultureInfo.InvariantCulture)}]"));

        var meters = points.Zip(points.Skip(1), Meters).Sum().ToString("0.##", CultureInfo.InvariantCulture);

        return $$$"""{"code":"Ok","routes":[{"distance":{{{meters}}},"duration":600,"geometry":{"coordinates":[{{{coordinates}}}]}}]}""";
    }
}
