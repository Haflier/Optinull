using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Routing;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Tests.Maps;

public sealed class MapCommandTests
{
    private static (BotCommandProcessor Processor, FakeRouting Routing, JobQueue Queue) Create(bool withMap = true)
    {
        var routing = new FakeRouting();
        var queue = new JobQueue();

        return (new BotCommandProcessor(queue, null, null, withMap ? routing : null), routing, queue);
    }

    // ---- Parser

    [Fact]
    public void Solve_WithMapKeyword_IsAMapCommand()
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse("/solve vrp map ga iterations=500 seed=7"));

        Assert.True(command.OnMap);
        Assert.Equal("vrp", command.Target);
        Assert.Equal(SolverKind.GeneticAlgorithm, command.Solver);
        Assert.Equal(500, command.Iterations);
        Assert.Equal(7, command.Seed);
    }

    [Theory]
    [InlineData("/solve vrp map all")]
    [InlineData("/solve vrp all map")]
    [InlineData("/compare vrp map")]
    public void Map_CannotBeCombinedWithCompare(string text) =>
        Assert.IsType<InvalidCommand>(BotCommandParser.Parse(text));

    // ---- /solve vrp map

    [Fact]
    public async Task SolveVrpMap_AsksForTheDepot_WithoutQueueingAJob()
    {
        var (processor, routing, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map greedy", 42, replies);

        Assert.Contains("depot", replies.Texts[^1]);
        Assert.False(queue.Reader.TryRead(out _));
        Assert.Equal(0, routing.MatrixCalls);
    }

    [Theory]
    [InlineData("/solve tsp map")]
    [InlineData("/solve vrp15 map")]
    public async Task Map_IsRefused_ForProblemsWithoutAMap(string text)
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync(text, 1, replies);

        Assert.Contains("cannot be solved on a map", replies.Texts[^1]);
    }

    [Fact]
    public async Task SolveVrpMap_RejectsSettingsAndInstanceText()
    {
        var (processor, _, _) = Create();

        var settings = new FakeReplies();
        await processor.ProcessAsync("/solve vrp map customers=20", 1, settings);

        var lines = new FakeReplies();
        await processor.ProcessAsync("/solve vrp map\n3 30\n50 50\n20 30 7", 1, lines);

        Assert.Contains("takes no instance lines or settings", settings.Texts[^1]);
        Assert.Contains("takes no instance lines or settings", lines.Texts[^1]);
    }

    [Fact]
    public async Task WithoutRouting_TheMapIsNotAvailable()
    {
        var (processor, _, _) = Create(withMap: false);
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map", 1, replies);

        Assert.Contains("not available", replies.Texts[^1]);
    }

    [Fact]
    public async Task ALocation_WithoutAMapDialog_IsNotUsed()
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessLocationAsync(52.5, 13.4, 1, replies);

        Assert.Contains("/solve vrp map", replies.Texts[^1]);
    }

    // ---- The whole conversation

    [Fact]
    public async Task Locations_ThenFleet_ThenSolve_QueuesOneJob_AndTheJobSendsThePng()
    {
        var (processor, routing, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map greedy", 7, replies);
        await processor.ProcessLocationAsync(52.50, 13.40, 7, replies);
        await processor.ProcessLocationAsync(52.51, 13.41, 7, replies);
        await processor.ProcessAsync("4", 7, replies);
        await processor.ProcessLocationAsync(52.52, 13.42, 7, replies);
        await processor.ProcessLocationAsync(52.49, 13.38, 7, replies);
        await processor.ProcessCallbackAsync("m:done", 7, replies);
        await processor.ProcessAsync("2 6", 7, replies);

        Assert.Equal(MapButtons("m:solve", "m:undo"), replies.Choices[^1].Choices.Select(c => c.Data).ToArray());

        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.Equal(1, routing.MatrixCalls);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job!);

        Assert.Equal(JobStatus.Completed, job!.Status);
        Assert.Contains(replies.Pngs, png => png.FileName == "map-routes.png" && png.Length > 1000);
        Assert.Contains(replies.Pngs, png => png.FileName == "map-convergence.png");
        Assert.True(routing.RouteCalls >= 1);
    }

    [Fact]
    public async Task WhenTheRoadShapeIsMissing_TheJobStillSendsThePng_WithAWarning()
    {
        var (processor, routing, queue) = Create();
        var replies = new FakeReplies();

        routing.RouteFailure = new RoutingException("down");

        await processor.ProcessAsync("/solve vrp map greedy", 7, replies);
        await processor.ProcessAsync("52.50, 13.40\n52.51, 13.41 2\n52.52, 13.42 3\ndone\n2 6", 7, replies);
        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job!);

        Assert.Equal(JobStatus.Completed, job!.Status);

        var routes = Assert.Single(replies.Pngs, png => png.FileName == "map-routes.png");
        Assert.Contains("straight lines", routes.Caption);
    }

    [Fact]
    public async Task WithTiles_TheMapIsDrawnOverThem_AndTheCaptionStaysClean()
    {
        var routing = new FakeRouting();
        var depot = new RoutePoint(13.40, 52.50);

        // Any valid PNG will do as a tile.
        var image = new RoadRouteChartRenderer().Render(
            depot,
            [new RoadChartRoute([depot, new(13.41, 52.51)], [new(13.41, 52.51)], "x")],
            "tile");

        var tiles = new FakeTiles(image);
        var queue = new JobQueue();
        var processor = new BotCommandProcessor(queue, null, null, routing, tiles);
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map greedy", 7, replies);
        await processor.ProcessAsync("52.50, 13.40\n52.51, 13.41\ndone\n1 5", 7, replies);
        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job!);

        Assert.Equal(JobStatus.Completed, job!.Status);
        Assert.True(tiles.Calls > 0);

        var routes = Assert.Single(replies.Pngs, png => png.FileName == "map-routes.png");
        Assert.DoesNotContain("background", routes.Caption);
    }

    [Fact]
    public async Task WhenTilesAreMissing_TheMapIsStillSent_AndTheCaptionSaysSo()
    {
        var routing = new FakeRouting();
        var tiles = new FakeTiles(null);
        var queue = new JobQueue();
        var processor = new BotCommandProcessor(queue, null, null, routing, tiles);
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map greedy", 7, replies);
        await processor.ProcessAsync("52.50, 13.40\n52.51, 13.41\ndone\n1 5", 7, replies);
        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job!);

        Assert.Equal(JobStatus.Completed, job!.Status);

        // It gives up after a few failures instead of waiting on every tile.
        Assert.Equal(3, tiles.Calls);

        var routes = Assert.Single(replies.Pngs, png => png.FileName == "map-routes.png");
        Assert.Contains("Map background incomplete", routes.Caption);
    }

    [Fact]
    public async Task WhenRoutingFails_NothingIsQueued_AndTheUserCanTryAgain()
    {
        var (processor, routing, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map greedy", 7, replies);
        await processor.ProcessAsync("52.50, 13.40\n52.51, 13.41\ndone\n1 5", 7, replies);

        routing.MatrixFailure = new RoutingException("down");
        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.False(queue.Reader.TryRead(out _));
        Assert.Contains(replies.Choices[^1].Choices, choice => choice.Data == "m:solve");

        routing.MatrixFailure = null;
        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.True(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task AnUnreachablePlace_NamesIt_AndKeepsTheDialog()
    {
        var (processor, routing, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map greedy", 7, replies);
        await processor.ProcessAsync("52.50, 13.40\n52.51, 13.41\ndone\n1 5", 7, replies);

        routing.MatrixFailure = new UnreachableRouteException(0, 1);
        await processor.ProcessCallbackAsync("m:solve", 7, replies);

        Assert.False(queue.Reader.TryRead(out _));
        Assert.Contains("customer 1", replies.Choices[^1].Text);
    }

    [Fact]
    public async Task Cancel_DropsTheMapDialog()
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map", 5, replies);
        await processor.ProcessAsync("/cancel", 5, replies);

        Assert.Contains("Cancelled", replies.Texts[^1]);

        await processor.ProcessLocationAsync(52.5, 13.4, 5, replies);

        Assert.Contains("not expecting", replies.Texts[^1]);
    }

    // ---- /optimize wizard

    [Fact]
    public async Task Wizard_OffersMapForVrpOnly()
    {
        var (processor, _, _) = Create();

        var vrp = new FakeReplies();
        await processor.ProcessCallbackAsync("o:vrp", 1, vrp);

        var tsp = new FakeReplies();
        await processor.ProcessCallbackAsync("o:tsp", 1, tsp);

        Assert.Equal(
            new[] { "o:vrp:small", "o:vrp:medium", "o:vrp:large", "o:vrp:map", "o:vrp:custom" },
            vrp.Choices[^1].Choices.Select(choice => choice.Data).ToArray());
        Assert.DoesNotContain(tsp.Choices[^1].Choices, choice => choice.Data.EndsWith(":map"));
    }

    [Fact]
    public async Task Wizard_WithoutRouting_KeepsTheOriginalDatasets()
    {
        var (processor, _, _) = Create(withMap: false);
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:vrp", 1, replies);

        Assert.Equal(
            new[] { "o:vrp:small", "o:vrp:medium", "o:vrp:large", "o:vrp:custom" },
            replies.Choices[^1].Choices.Select(choice => choice.Data).ToArray());
    }

    [Fact]
    public async Task Wizard_Map_OffersEverySolverExceptAll()
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:vrp:map", 1, replies);

        Assert.Equal(
            new[] { "o:vrp:map:greedy", "o:vrp:map:hc", "o:vrp:map:ils", "o:vrp:map:sa", "o:vrp:map:ga" },
            replies.Choices[^1].Choices.Select(choice => choice.Data).ToArray());
    }

    [Fact]
    public async Task Wizard_PickingASolver_StartsTheDialog()
    {
        var (processor, _, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:vrp:map:ga", 9, replies);

        Assert.Contains("depot", replies.Texts[^1]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Wizard_StaleMapAllButton_IsRefused()
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:vrp:map:all", 1, replies);

        Assert.Contains("out of date", replies.Texts[^1]);
    }

    [Fact]
    public async Task MapButtons_WithoutADialog_AreStale()
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("m:solve", 1, replies);

        Assert.Contains("out of date", replies.Texts[^1]);
    }

    private static string[] MapButtons(params string[] data) => data;
}
