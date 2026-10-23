using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Infrastructure.Routing;
using Optinull.Integration.Tests.Support;

namespace Optinull.Integration.Tests.Maps;

/// <summary>
/// The whole /solve vrp map flow with the real bot, queue, executor, OSRM client, tile client and
/// renderer. Only the network is replaced: one stub plays both the routing and the tile server.
/// </summary>
public sealed class MapFlowIntegrationTests
{
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
            await Processor.ProcessAsync(World.Places, chat, Replies);
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

    [Fact]
    public async Task FromLocationsToAPngOnAMap()
    {
        var world = World.Create();
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
        var withTiles = World.Create();
        var botWith = new Bot(withTiles);

        await botWith.EnterAsync();
        await botWith.PressSolveAsync();
        await botWith.RunQueuedJobAsync();

        var without = World.Create();
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
        var world = World.Create();

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
        var world = World.Create();

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
        var world = World.Create();

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
        var world = World.Create();
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
        var bot = new Bot(World.Create());

        await bot.Processor.ProcessAsync("/solve vrp map greedy", 1, bot.Replies);
        await bot.Processor.ProcessAsync("/solve vrp map greedy", 2, bot.Replies);

        await bot.Processor.ProcessAsync(World.Places, 1, bot.Replies);
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

    // ---- A busy chat, and cancelling

    [Fact]
    public async Task WhenTheChatIsBusy_SolveIsRefused_AndThePlacesAreKeptForLater()
    {
        var bot = new Bot(World.Create());

        // The first map's job waits in the queue, so the chat counts as busy.
        await bot.EnterAsync();
        await bot.PressSolveAsync();

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        Assert.Contains(bot.Replies.Texts, text => text.Contains("already have job #1"));

        Assert.True(bot.Queue.Reader.TryRead(out var first));
        Assert.False(bot.Queue.Reader.TryRead(out _));

        await new JobExecutor().ExecuteAsync(first!);

        Assert.Equal(JobStatus.Completed, first!.Status);

        // The second map was not lost: Solve works now without entering the places again.
        await bot.PressSolveAsync();

        var second = await bot.RunQueuedJobAsync();

        Assert.Equal(JobStatus.Completed, second.Status);
        Assert.Equal(2, bot.Replies.Pngs.Count(png => png.FileName == "map-routes.png"));
    }

    [Fact]
    public async Task CancellingWhileTheTilesLoad_StopsTheJob_SendsNoMap_AndFreesTheChat()
    {
        var world = World.Create();

        world.TilesBlock = true;

        var bot = new Bot(world);

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        Assert.True(bot.Queue.Reader.TryRead(out var job));

        var run = new JobExecutor().ExecuteAsync(job!);

        await Waiting.UntilAsync(() => !world.TileRequests.IsEmpty, "the first tile to be requested");

        await bot.Processor.ProcessAsync("/cancel", 1, bot.Replies);
        await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(JobStatus.Cancelled, job!.Status);
        Assert.Contains(bot.Replies.Texts, text => text.Contains("cancelled"));
        Assert.DoesNotContain(bot.Replies.Pngs, png => png.FileName == "map-routes.png");

        // The chat is free again: the same places can be solved right away.
        world.TilesBlock = false;

        await bot.EnterAsync();
        await bot.PressSolveAsync();

        var again = await bot.RunQueuedJobAsync();

        Assert.Equal(JobStatus.Completed, again.Status);
        Assert.Contains(bot.Replies.Pngs, png => png.FileName == "map-routes.png");
    }
}
