using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.MapSessions;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests.MapSessions;

public sealed class MapCommandTests
{
    private sealed class FakeLauncher : IMapSessionLauncher
    {
        public List<(long Owner, long Chat, MapSettings Settings)> Opened { get; } = [];

        public bool CloseResult { get; set; }

        public List<long> Closed { get; } = [];

        public MapLink Open(long ownerId, long chatId, IBotReplies replies, MapSettings settings)
        {
            Opened.Add((ownerId, chatId, settings));
            return new MapLink("https://example.test/#s=abc", TimeSpan.FromMinutes(30));
        }

        public bool Close(long ownerId)
        {
            Closed.Add(ownerId);
            return CloseResult;
        }
    }

    private static (BotCommandProcessor Processor, FakeLauncher Launcher, JobQueue Queue) Create(bool withMap = true)
    {
        var launcher = new FakeLauncher();
        var queue = new JobQueue();

        return (new BotCommandProcessor(queue, null, null, withMap ? launcher : null), launcher, queue);
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

    [Fact]
    public void Solve_WithMapKeyword_DefaultsToSimulatedAnnealing()
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse("/solve vrp MAP"));

        Assert.True(command.OnMap);
        Assert.Equal(SolverKind.SimulatedAnnealing, command.Solver);
    }

    [Fact]
    public void Solve_WithoutMapKeyword_IsNotAMapCommand() =>
        Assert.False(Assert.IsType<SolveCommand>(BotCommandParser.Parse("/solve vrp ga")).OnMap);

    [Theory]
    [InlineData("/solve vrp map all")]
    [InlineData("/solve vrp all map")]
    [InlineData("/compare vrp map")]
    public void Map_CannotBeCombinedWithCompare(string text) =>
        Assert.IsType<InvalidCommand>(BotCommandParser.Parse(text));

    // ---- /solve vrp map

    [Fact]
    public async Task SolveVrpMap_OpensASession_AndSendsTheLink_WithoutQueueingAJob()
    {
        var (processor, launcher, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map ils iterations=2000", 42, replies);

        var opened = Assert.Single(launcher.Opened);
        Assert.Equal(42, opened.Owner);
        Assert.Equal(42, opened.Chat);
        Assert.Equal(SolverKind.IteratedLocalSearch, opened.Settings.Solver);
        Assert.Equal(2000, opened.Settings.Iterations);
        Assert.Equal(SolveOptions.DefaultSeed, opened.Settings.Seed);

        Assert.Contains("https://example.test/#s=abc", replies.Texts[^1]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task SolveVrpMap_IsRefusedInAGroupChat()
    {
        var (processor, launcher, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map", -1001234, replies);

        Assert.Empty(launcher.Opened);
        Assert.Contains("private chat", replies.Texts[^1]);
    }

    [Theory]
    [InlineData("/solve tsp map")]
    [InlineData("/solve vrp15 map")]
    public async Task Map_IsRefused_ForProblemsWithoutAMap(string text)
    {
        var (processor, launcher, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync(text, 1, replies);

        Assert.Empty(launcher.Opened);
        Assert.Contains("cannot be solved on a map", replies.Texts[^1]);
    }

    [Fact]
    public async Task SolveVrpMap_RejectsSettingsAndInstanceText()
    {
        var (processor, launcher, _) = Create();

        var settings = new FakeReplies();
        await processor.ProcessAsync("/solve vrp map customers=20", 1, settings);

        var lines = new FakeReplies();
        await processor.ProcessAsync("/solve vrp map\n3 30\n50 50\n20 30 7", 1, lines);

        Assert.Empty(launcher.Opened);
        Assert.Contains("takes no instance lines or settings", settings.Texts[^1]);
        Assert.Contains("takes no instance lines or settings", lines.Texts[^1]);
    }

    [Fact]
    public async Task WithoutALauncher_TheMapIsNotAvailable()
    {
        var (processor, _, _) = Create(withMap: false);
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp map", 1, replies);

        Assert.Contains("not available", replies.Texts[^1]);
    }

    // ---- /optimize wizard

    [Fact]
    public async Task Wizard_OffersMapForVrpOnly_InAPrivateChat()
    {
        var (processor, _, _) = Create();

        var vrp = new FakeReplies();
        await processor.ProcessCallbackAsync("o:vrp", 1, vrp);

        var tsp = new FakeReplies();
        await processor.ProcessCallbackAsync("o:tsp", 1, tsp);

        var group = new FakeReplies();
        await processor.ProcessCallbackAsync("o:vrp", -5, group);

        Assert.Equal(
            new[] { "o:vrp:small", "o:vrp:medium", "o:vrp:large", "o:vrp:map", "o:vrp:custom" },
            vrp.Choices[^1].Choices.Select(choice => choice.Data).ToArray());
        Assert.DoesNotContain(tsp.Choices[^1].Choices, choice => choice.Data.EndsWith(":map"));
        Assert.DoesNotContain(group.Choices[^1].Choices, choice => choice.Data.EndsWith(":map"));
    }

    [Fact]
    public async Task Wizard_WithoutALauncher_KeepsTheOriginalDatasets()
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
    public async Task Wizard_PickingASolver_OpensTheSession_WithThatSolver()
    {
        var (processor, launcher, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:vrp:map:ga", 9, replies);

        var opened = Assert.Single(launcher.Opened);
        Assert.Equal(9, opened.Owner);
        Assert.Equal(SolverKind.GeneticAlgorithm, opened.Settings.Solver);
        Assert.Contains("#s=abc", replies.Texts[^1]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Wizard_StaleMapAllButton_IsRefused()
    {
        var (processor, launcher, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:vrp:map:all", 1, replies);

        Assert.Empty(launcher.Opened);
        Assert.Contains("out of date", replies.Texts[^1]);
    }

    // ---- /cancel

    [Fact]
    public async Task Cancel_ClosesTheMap_WhenThereIsNoJobToStop()
    {
        var (processor, launcher, _) = Create();
        launcher.CloseResult = true;
        var replies = new FakeReplies();

        await processor.ProcessAsync("/cancel", 5, replies);

        Assert.Equal(new long[] { 5 }, launcher.Closed);
        Assert.Contains("Map closed", replies.Texts[^1]);
    }

    [Fact]
    public async Task Cancel_WithNothingOpen_KeepsTheOriginalMessage()
    {
        var (processor, _, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/cancel", 5, replies);

        Assert.Equal("You have no job in progress.", replies.Texts[^1]);
    }
}
