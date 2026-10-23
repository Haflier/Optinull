using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Infrastructure.Routing;
using Optinull.Reporting.Charts;

namespace Optinull.Integration.Tests.Maps;

/// <summary>
/// The whole /solve vrp map flow with the real bot, queue, executor, OSRM client, tile client and
/// renderer. Only the network is replaced: one stub plays both the routing and the tile server.
/// </summary>
public sealed class MapFlowIntegrationTests
{
    // Six places about 5 km apart, so the picture needs several tiles.
    private const string Places =
        "52.50, 13.40\n" +
        "52.52, 13.37 4\n" +
        "52.54, 13.41 5\n" +
        "52.53, 13.45 3\n" +
        "52.49, 13.44 4\n" +
        "52.48, 13.38 3\n" +
        "52.51, 13.34 4\n" +
        "done\n" +
        "2 14";

    private sealed class World : HttpMessageHandler
    {
        public World(byte[] tile) => Tile = tile;

        public byte[] Tile { get; }

        public bool TilesDown { get; set; }

        public bool TableDown { get; set; }

        public bool RouteDown { get; set; }

        public ConcurrentQueue<Uri> TileRequests { get; } = new();

        public ConcurrentQueue<Uri> Tables { get; } = new();

        public ConcurrentQueue<Uri> Routes { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;

            if (uri.Host == "tiles.test")
            {
                TileRequests.Enqueue(uri);

                return Task.FromResult(TilesDown
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Tile) });
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

                return Task.FromResult(TableDown ? Down() : Json(Table(points)));
            }

            Routes.Enqueue(uri);

            return Task.FromResult(RouteDown ? Down() : Json(Route(points)));
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

    private sealed class CapturingReplies : IBotReplies
    {
        private readonly ConcurrentQueue<string> _texts = new();
        private readonly ConcurrentQueue<(string FileName, string Caption, int Length)> _pngs = new();
        private readonly ConcurrentQueue<(string Text, IReadOnlyList<Choice> Choices)> _choices = new();

        public IReadOnlyList<string> Texts => _texts.ToArray();

        public IReadOnlyList<(string FileName, string Caption, int Length)> Pngs => _pngs.ToArray();

        public IReadOnlyList<(string Text, IReadOnlyList<Choice> Choices)> Choices => _choices.ToArray();

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            _texts.Enqueue(text);
            return Task.CompletedTask;
        }

        public Task SendPngAsync(byte[] png, string fileName, string caption, CancellationToken cancellationToken)
        {
            _pngs.Enqueue((fileName, caption, png.Length));
            return Task.CompletedTask;
        }

        public Task ShowChoicesAsync(
            string text,
            IReadOnlyList<IReadOnlyList<Choice>> rows,
            CancellationToken cancellationToken)
        {
            _choices.Enqueue((text, rows.SelectMany(row => row).ToList()));
            return Task.CompletedTask;
        }
    }

    private sealed class Bot
    {
        public Bot(World world)
        {
            World = world;
            Queue = new JobQueue();

            var routing = new OsrmRoutingClient(
                new HttpClient(world),
                new OsrmOptions { BaseUrl = "https://osrm.test" });

            var tiles = new HttpMapTileClient(
                new HttpClient(world),
                new MapTileOptions { UrlTemplate = "https://tiles.test/{z}/{x}/{y}.png", Attribution = "\u00a9 Test map" });

            Processor = new BotCommandProcessor(Queue, null, null, routing, tiles);
        }

        public World World { get; }

        public JobQueue Queue { get; }

        public BotCommandProcessor Processor { get; }

        public CapturingReplies Replies { get; } = new();

        public async Task EnterAsync(long chat = 1)
        {
            await Processor.ProcessAsync("/solve vrp map greedy", chat, Replies);
            await Processor.ProcessAsync(Places, chat, Replies);
        }

        public Task PressSolveAsync(long chat = 1) =>
            Processor.ProcessCallbackAsync("m:solve", chat, Replies);

        public async Task<OptimizationJob> RunQueuedJobAsync()
        {
            Assert.True(Queue.Reader.TryRead(out var job));

            await new JobExecutor().ExecuteAsync(job!);

            return job!;
        }
    }

    private static World NewWorld()
    {
        // Any valid PNG will do as a tile.
        var depot = new RoutePoint(13.40, 52.50);

        var tile = new RoadRouteChartRenderer().Render(
            depot,
            [new RoadChartRoute([depot, new(13.41, 52.51)], [new(13.41, 52.51)], "tile")],
            "tile");

        return new World(tile);
    }

    [Fact]
    public async Task FromLocationsToAPngOnAMap()
    {
        var world = NewWorld();
        var bot = new Bot(world);

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        var job = await bot.RunQueuedJobAsync();

        Assert.Equal(JobStatus.Completed, job.Status);

        // One road-distance matrix, one road shape per truck, and the tiles under the picture.
        Assert.Single(world.Tables);
        Assert.InRange(world.Routes.Count, 1, 2);
        Assert.True(world.TileRequests.Count >= 4);
        Assert.All(world.TileRequests, uri => Assert.Matches(@"^/\d+/\d+/\d+\.png$", uri.AbsolutePath));

        var map = Assert.Single(bot.Replies.Pngs, png => png.FileName == "map-routes.png");

        Assert.True(map.Length > 10_000);
        Assert.DoesNotContain("straight lines", map.Caption);
        Assert.DoesNotContain("background", map.Caption);
        Assert.Contains(bot.Replies.Pngs, png => png.FileName == "map-convergence.png");
    }

    [Fact]
    public async Task AMapWithTiles_IsNotTheSameImageAsOneWithout()
    {
        var withTiles = NewWorld();
        var botWith = new Bot(withTiles);

        await botWith.EnterAsync();
        await botWith.PressSolveAsync();
        await botWith.RunQueuedJobAsync();

        var without = NewWorld();
        var botWithout = new Bot(without);

        without.TilesDown = true;

        await botWithout.EnterAsync();
        await botWithout.PressSolveAsync();
        await botWithout.RunQueuedJobAsync();

        Assert.NotEqual(
            botWith.Replies.Pngs.Single(png => png.FileName == "map-routes.png").Length,
            botWithout.Replies.Pngs.Single(png => png.FileName == "map-routes.png").Length);
    }

    [Fact]
    public async Task WhenTheTileServerIsDown_TheMapIsStillSent_AfterThreeTries()
    {
        var world = NewWorld();

        world.TilesDown = true;

        var bot = new Bot(world);

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        var job = await bot.RunQueuedJobAsync();

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(3, world.TileRequests.Count);

        var map = Assert.Single(bot.Replies.Pngs, png => png.FileName == "map-routes.png");

        Assert.Contains("Map background incomplete", map.Caption);
    }

    [Fact]
    public async Task WhenTheRoadShapeIsDown_TheTrucksAreDrawnAsStraightLines()
    {
        var world = NewWorld();

        world.RouteDown = true;

        var bot = new Bot(world);

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        var job = await bot.RunQueuedJobAsync();

        Assert.Equal(JobStatus.Completed, job.Status);

        var map = Assert.Single(bot.Replies.Pngs, png => png.FileName == "map-routes.png");

        Assert.Contains("straight lines", map.Caption);
        Assert.True(world.TileRequests.Count >= 4);
    }

    [Fact]
    public async Task WhenTheDistanceServiceIsDown_NothingIsQueued_AndTryAgainWorks()
    {
        var world = NewWorld();

        world.TableDown = true;

        var bot = new Bot(world);

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        Assert.False(bot.Queue.Reader.TryRead(out _));
        Assert.Contains(bot.Replies.Choices[^1].Choices, choice => choice.Data == "m:solve");

        world.TableDown = false;

        await bot.PressSolveAsync();

        var job = await bot.RunQueuedJobAsync();

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Contains(bot.Replies.Pngs, png => png.FileName == "map-routes.png");
    }

    [Fact]
    public async Task SolvingTheSamePlacesAgain_ReusesTheMatrixAndTheTiles()
    {
        var world = NewWorld();
        var bot = new Bot(world);

        await bot.EnterAsync();
        await bot.PressSolveAsync();
        await bot.RunQueuedJobAsync();

        var tiles = world.TileRequests.Count;

        await bot.EnterAsync();
        await bot.PressSolveAsync();
        await bot.RunQueuedJobAsync();

        Assert.Single(world.Tables);
        Assert.Equal(tiles, world.TileRequests.Count);
        Assert.Equal(2, bot.Replies.Pngs.Count(png => png.FileName == "map-routes.png"));
    }

    [Fact]
    public async Task TwoChatsCanBuildTheirOwnMaps_AtTheSameTime()
    {
        var world = NewWorld();
        var bot = new Bot(world);

        await bot.Processor.ProcessAsync("/solve vrp map greedy", 1, bot.Replies);
        await bot.Processor.ProcessAsync("/solve vrp map greedy", 2, bot.Replies);

        await bot.Processor.ProcessAsync(Places, 1, bot.Replies);
        await bot.Processor.ProcessAsync("52.50, 13.40\n52.51, 13.41\ndone\n1 5", 2, bot.Replies);

        await bot.PressSolveAsync(1);
        await bot.PressSolveAsync(2);

        Assert.True(bot.Queue.Reader.TryRead(out var first));
        Assert.True(bot.Queue.Reader.TryRead(out var second));

        await new JobExecutor().ExecuteAsync(first!);
        await new JobExecutor().ExecuteAsync(second!);

        Assert.Equal(JobStatus.Completed, first!.Status);
        Assert.Equal(JobStatus.Completed, second!.Status);
        Assert.Equal(2, bot.Replies.Pngs.Count(png => png.FileName == "map-routes.png"));
    }
}
