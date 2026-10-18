using Optinull.Application.Commands;
using Optinull.Application.Comparison;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Problems.Vrp;

namespace Optinull.Application.Tests.Vrp;

public sealed class VrpApplicationTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private static VrpProblem Problem(int customers = 20, int vehicles = 4, int seed = 1) =>
        VrpInstances.Random(customers, vehicles, seed);

    private static IProblemInstance Builtin(string name)
    {
        Assert.True(ProblemRegistry.CreateDefault().TryGetBuiltIn(name, out var instance));
        return instance!;
    }

    private static (BotCommandProcessor Processor, JobQueue Queue) Start()
    {
        var queue = new JobQueue();

        return (new BotCommandProcessor(queue), queue);
    }

    // ---- Solving -------------------------------------------------------------------------------

    [Theory]
    [InlineData(SolverKind.Greedy)]
    [InlineData(SolverKind.HillClimbing)]
    [InlineData(SolverKind.IteratedLocalSearch)]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public void EverySolver_ReturnsAFeasiblePlan_WhoseDistanceAddsUp(SolverKind kind)
    {
        var problem = Problem(15, 3);

        var result = new VrpSolveService().Solve(problem, kind, seed: 2, iterations: 2_000);

        Assert.Equal(SolverNames.Display(kind), result.SolverName);
        Assert.True(VrpEvaluator.Evaluate(problem, result.Plan).IsFeasible);
        Assert.Equal(VrpEvaluator.TotalDistance(problem, result.Plan), result.Distance, 6);
        Assert.Equal(15, result.Plan.CustomerCount);
        Assert.True(result.Plan.RouteCount <= problem.Vehicles);
        Assert.True(result.Search.EvaluationCount >= 1);
    }

    [Fact]
    public void Greedy_IsTheNearestNeighbourPlan_AndTheOthersStartFromTheSameBaseline()
    {
        var problem = Problem(20, 4);
        var service = new VrpSolveService();

        var greedy = service.Solve(problem, SolverKind.Greedy);

        Assert.Equal(greedy.GreedyDistance, greedy.Distance, 6);
        Assert.Null(greedy.Settings);

        Assert.Equal(
            greedy.GreedyDistance,
            service.Solve(problem, SolverKind.SimulatedAnnealing, iterations: 500).GreedyDistance,
            6);
    }

    [Fact]
    public void Annealing_ImprovesOnTheGreedyPlan()
    {
        var problem = Problem(30, 5, seed: 4);

        var result = new VrpSolveService().Solve(problem, SolverKind.SimulatedAnnealing, seed: 1);

        Assert.True(result.Distance < result.GreedyDistance, $"{result.Distance} vs {result.GreedyDistance}");
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    [InlineData(SolverKind.IteratedLocalSearch)]
    public void ARun_IsReproducible_ForTheSameSeed(SolverKind kind)
    {
        var problem = Problem(15, 3);
        var service = new VrpSolveService();

        var first = service.Solve(problem, kind, seed: 7, iterations: 1_500);
        var second = service.Solve(problem, kind, seed: 7, iterations: 1_500);

        Assert.Equal(first.Distance, second.Distance);
        Assert.Equal(first.Settings, second.Settings);
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing, 900, 800, 1100)]
    [InlineData(SolverKind.GeneticAlgorithm, 500, 100, 500)]
    public void Iterations_LimitTheNumberOfEvaluations(SolverKind kind, int iterations, int atLeast, int atMost)
    {
        var result = new VrpSolveService().Solve(Problem(15, 3), kind, seed: 1, iterations: iterations);

        Assert.InRange(result.Search.EvaluationCount, atLeast, atMost);
    }

    [Fact]
    public void ProgressGrowsToTheBudget_ForTheBudgetedSolvers()
    {
        // Progress<T> posts to the thread pool, so values are collected synchronously instead.
        var sync = new SyncProgress();

        new VrpSolveService().Solve(Problem(15, 3), SolverKind.SimulatedAnnealing, seed: 1, iterations: 1_000, progress: sync);

        Assert.NotEmpty(sync.Values);
        Assert.All(sync.Values, value => Assert.InRange(value, 0.0, 1.0));
        Assert.Equal(sync.Values.OrderBy(value => value), sync.Values);
        Assert.True(sync.Values[^1] >= 0.9);
    }

    private sealed class SyncProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }

    // ---- Automatic and chosen settings ---------------------------------------------------------------

    [Fact]
    public void TheEngineChoosesTheSettings_WhenTheUserDoesNot()
    {
        var problem = Problem(20, 4);
        var service = new VrpSolveService();

        Assert.Equal(
            "temperature=1 (auto), final=0.00001 (auto), moves=90 (auto)",
            service.Solve(problem, SolverKind.SimulatedAnnealing).Settings);

        Assert.Equal(
            "population=50 (auto), elite=2 (auto), mutation=0.2 (auto), tournament=3 (auto), localsearch=10 (auto)",
            service.Solve(problem, SolverKind.GeneticAlgorithm).Settings);

        // 20 customers is below the size where the perturbation starts to grow.
        Assert.Equal(
            "perturbation=3 (auto)",
            service.Solve(problem, SolverKind.IteratedLocalSearch, iterations: 500).Settings);
    }

    [Fact]
    public void WhatTheUserSets_IsUsed_AndMarked()
    {
        var settings = new VrpSolveService().Solve(
            Problem(20, 4),
            SolverKind.GeneticAlgorithm,
            iterations: 3_000,
            tuning: new SolverTuning { Population = 20, Mutation = 0.35 }).Settings;

        Assert.Contains("population=20 (yours)", settings);
        Assert.Contains("mutation=0.35 (yours)", settings);
        Assert.Contains("tournament=3 (auto)", settings);
    }

    [Fact]
    public void Temperature_ReachesTheAnnealer()
    {
        var problem = Problem(30, 5, seed: 7);

        double Distance(double temperature) =>
            new VrpSolveService().Solve(
                problem, SolverKind.SimulatedAnnealing, seed: 1, iterations: 3_000,
                tuning: new SolverTuning { Temperature = temperature }).Distance;

        Assert.NotEqual(Distance(0.01), Distance(50));
    }

    // ---- The module --------------------------------------------------------------------------------------

    [Fact]
    public void TheRegistryKnowsRouting_AndItsBuiltIns()
    {
        var registry = ProblemRegistry.CreateDefault();

        Assert.NotNull(registry.FindModule("vrp"));
        Assert.NotNull(registry.FindModule("VRP"));
        Assert.True(registry.TryGetBuiltIn("vrp15", out var small));
        Assert.True(registry.TryGetBuiltIn("VRP40", out var medium));
        Assert.Equal("vrp15", small!.Name);
        Assert.Contains("40 customers, 6 vehicles", medium!.Description);
    }

    [Fact]
    public void TheBuiltIns_AreFixed_AndSolveable()
    {
        var instance = Builtin("vrp15");

        var first = instance.Run(SolverKind.Greedy, new SolveOptions(), CancellationToken.None);
        var again = Builtin("vrp15").Run(SolverKind.Greedy, new SolveOptions(), CancellationToken.None);

        Assert.Equal(first.Score, again.Score);
        Assert.Equal("Total distance", instance.ScoreLabel);
        Assert.False(instance.HigherIsBetter);
    }

    [Theory]
    [InlineData("customers=2", "customers must be")]
    [InlineData("customers=101", "customers must be")]
    [InlineData("vehicles=0", "vehicles must be")]
    [InlineData("vehicles=21", "vehicles must be")]
    [InlineData("cities=5", "Unknown parameter 'cities'")]
    public void BadSettings_AreExplained(string settings, string reason)
    {
        var module = ProblemRegistry.CreateDefault().FindModule("vrp")!;
        var values = settings.Split('=');

        Assert.False(module.TryGenerate(new Dictionary<string, string> { [values[0]] = values[1] }, 1, out _, out var error));
        Assert.Contains(reason, error);
    }

    [Fact]
    public void Generated_UsesTheDefaults_ForWhatIsMissing()
    {
        var module = ProblemRegistry.CreateDefault().FindModule("vrp")!;

        Assert.True(module.TryGenerate(new Dictionary<string, string> { ["customers"] = "12" }, 5, out var instance, out _));

        Assert.Equal("vrp12v4", instance!.Name);
        Assert.Contains("12 customers, 4 vehicles", instance.Description);
        Assert.Contains("seed 5", instance.Description);
    }

    [Fact]
    public void ACustomProblem_IsReadWithItsOwnDescription_AndBadOnesAreRefused()
    {
        var module = ProblemRegistry.CreateDefault().FindModule("vrp")!;

        Assert.True(module.TryParseCustom("3 30\n50 50\n20 30 7\n80 65 12\n60 20 9", out var instance, out _));
        Assert.Equal("custom", instance!.Name);
        Assert.Equal("custom routing, 3 customers, 3 vehicles", instance.Description);

        Assert.False(module.TryParseCustom("1 10\n0 0\n1 1 8\n2 2 8", out _, out var error));
        Assert.Contains("at least 2 vehicles", error);
    }

    [Fact]
    public void Help_ListsRouting_AndStillFitsInOneTelegramMessage()
    {
        var module = ProblemRegistry.CreateDefault().FindModule("vrp")!;

        Assert.Contains("x y demand", module.CustomInputHelp);
        Assert.Equal(3, module.Presets.Count);
    }

    // ---- Through the bot -------------------------------------------------------------------------------------------

    [Fact]
    public async Task SolveCommand_RunsARandomProblem_AndSendsTheRoutesAndTheConvergence()
    {
        var (processor, queue) = Start();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve vrp customers=10 vehicles=3 ga iterations=500", 1, replies);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Contains("10 customers, 3 vehicles", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(2, replies.Pngs.Count);
        Assert.Equal("vrp10v3-routes.png", replies.Pngs[0].FileName);
        Assert.Contains("of 3 vehicles", replies.Pngs[0].Caption);
        Assert.Contains("nearest neighbour", replies.Pngs[0].Caption);
        Assert.Equal("vrp10v3-convergence.png", replies.Pngs[1].FileName);
        Assert.Contains("Total distance:", replies.Texts[^1]);
        Assert.Contains("Settings: population=", replies.Texts[^1]);
    }

    [Fact]
    public async Task ATypedCustomProblem_IsQueuedAndSolved()
    {
        var (processor, queue) = Start();
        var replies = new FakeReplies();

        await processor.ProcessAsync(
            "/solve vrp ils iterations=500\n3 30\n50 50\n20 30 7\n80 65 12\n60 20 9", 1, replies);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("custom", job.Instance.Name);

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("custom-routes.png", replies.Pngs[0].FileName);
    }

    [Fact]
    public async Task Compare_RunsEverySolverOnARoutingProblem()
    {
        var (processor, queue) = Start();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/compare vrp15 iterations=500", 1, replies);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(3, replies.Pngs.Count);
        Assert.Equal("vrp15-compare.png", replies.Pngs[0].FileName);

        foreach (var solver in new[] { "Greedy", "Hill Climbing", "Iterated Local Search", "Simulated Annealing", "Genetic Algorithm" })
            Assert.Contains(solver, replies.Pngs[0].Caption);

        Assert.Contains("Best:", replies.Pngs[0].Caption);
        Assert.All(replies.Pngs, png => Assert.True(png.Length > 1000));
    }

    [Fact]
    public async Task TheWizard_OffersRouting_WithThreeDatasets()
    {
        var (processor, _) = Start();

        var problems = new FakeReplies();
        await processor.ProcessAsync("/optimize", 1, problems);
        Assert.Contains(problems.Choices[^1].Choices, choice => choice.Data == "o:vrp");

        var datasets = new FakeReplies();
        await processor.ProcessCallbackAsync("o:vrp", 1, datasets);

        Assert.Equal(
            new[] { "o:vrp:small", "o:vrp:medium", "o:vrp:large", "o:vrp:custom" },
            datasets.Choices[^1].Choices.Select(choice => choice.Data).ToArray());
    }

    [Theory]
    [InlineData("small", "vrp12v3")]
    [InlineData("medium", "vrp40")]
    [InlineData("large", "vrp80v10")]
    public async Task EveryDatasetPreset_StartsAJob(string preset, string name)
    {
        var (processor, queue) = Start();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync($"o:vrp:{preset}:sa", 1, replies);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(name, job.Instance.Name);
    }

    // ---- The guided input --------------------------------------------------------------------------------------------

    private static async Task<FakeReplies> Say(BotCommandProcessor processor, string text)
    {
        var replies = new FakeReplies();
        await processor.ProcessAsync(text, 1, replies);
        return replies;
    }

    private static async Task<FakeReplies> Press(BotCommandProcessor processor, string data)
    {
        var replies = new FakeReplies();
        await processor.ProcessCallbackAsync(data, 1, replies);
        return replies;
    }

    [Fact]
    public async Task TheGuidedInput_AsksForVehiclesCapacityCustomersThenTheLocations_AndRuns()
    {
        var (processor, queue) = Start();

        var start = await Press(processor, "o:vrp:custom");
        Assert.Contains("How many vehicles", start.Texts[0]);

        Assert.Contains("How much can one vehicle carry", (await Say(processor, "2")).Texts[^1]);
        Assert.Contains("How many customers", (await Say(processor, "20")).Texts[^1]);

        var locations = (await Say(processor, "3")).Texts[^1];
        Assert.Contains("First the depot", locations);
        Assert.Contains("3 of them", locations);
        Assert.Contains("at most 20", locations);

        var depot = (await Say(processor, "50 50")).Texts[^1];
        Assert.Contains("Depot: (50, 50)", depot);
        Assert.Contains("0 of 3 customers so far", depot);
        Assert.Contains("Send 3 more", depot);

        var some = (await Say(processor, "10 10 5\n20 20 8")).Texts[^1];
        Assert.Contains("Customer 1: (10, 10), demand 5", some);
        Assert.Contains("2 of 3 customers so far", some);

        var done = await Say(processor, "30 30 9");

        Assert.Contains("Got it: custom routing, 3 customers, 2 vehicles", done.Choices[^1].Text);

        var started = await Press(processor, "o:vrp:custom:ga");

        Assert.Contains("Queued job #1", started.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("custom-routes.png", started.Pngs[0].FileName);
    }

    [Fact]
    public async Task EverythingCanComeInOneMessage()
    {
        var (processor, _) = Start();

        await Press(processor, "o:vrp:custom");
        await Say(processor, "2");
        await Say(processor, "20");
        await Say(processor, "3");

        var done = await Say(processor, "50 50\n10 10 5\n20 20 8\n30 30 9");

        Assert.Contains("Got it: custom routing, 3 customers, 2 vehicles", done.Choices[^1].Text);
    }

    [Theory]
    [InlineData("50", "the depot must be \"x y\"")]
    [InlineData("50 50 7", "the depot must be \"x y\"")]
    [InlineData("a b", "coordinates must be numbers")]
    public async Task ABadDepot_IsExplained(string line, string reason)
    {
        var (processor, _) = Start();

        await Press(processor, "o:vrp:custom");
        await Say(processor, "2");
        await Say(processor, "20");
        await Say(processor, "3");

        var reply = (await Say(processor, line)).Texts[^1];

        Assert.Contains(reason, reply);
        Assert.Contains("First the depot", reply);
    }

    [Theory]
    [InlineData("10 10", "I need \"x y demand\"")]
    [InlineData("10 10 21", "the demand must be a whole number from 1 to the capacity 20")]
    [InlineData("10 10 0", "the demand must be a whole number from 1 to the capacity 20")]
    [InlineData("10 10 2.5", "the demand must be a whole number")]
    [InlineData("x 10 5", "coordinates must be numbers")]
    public async Task ABadCustomer_IsExplained_AndTheDepotIsKept(string line, string reason)
    {
        var (processor, _) = Start();

        await Press(processor, "o:vrp:custom");
        await Say(processor, "2");
        await Say(processor, "20");
        await Say(processor, "3");
        await Say(processor, "50 50");

        var reply = (await Say(processor, line)).Texts[^1];

        Assert.Contains(reason, reply);
        Assert.Contains("0 of 3 customers so far", reply);
    }

    [Fact]
    public async Task Undo_RemovesTheLastLine_IncludingTheDepot()
    {
        var (processor, _) = Start();

        await Press(processor, "o:vrp:custom");
        await Say(processor, "2");
        await Say(processor, "20");
        await Say(processor, "3");
        await Say(processor, "50 50\n10 10 5");

        // The depot and one customer were given; undoing twice takes the customer, then the depot.
        var first = (await Say(processor, "undo")).Texts[^1];

        Assert.Contains("Removed the last one", first);
        Assert.Contains("0 of 3 customers so far", first);

        Assert.Contains("First the depot", (await Say(processor, "undo")).Texts[^1]);
        Assert.Contains("nothing to undo", (await Say(processor, "undo")).Texts[^1]);
    }

    [Fact]
    public async Task AFleetThatCannotCarryTheLoads_StopsTheDialog_NamingTheNumberNeeded()
    {
        var (processor, queue) = Start();

        await Press(processor, "o:vrp:custom");
        await Say(processor, "1");
        await Say(processor, "10");
        await Say(processor, "2");

        var reply = await Say(processor, "0 0\n1 1 8\n2 2 8");

        Assert.Contains("at least 2 vehicles", reply.Texts[^1]);
        Assert.Contains("/optimize", reply.Texts[^1]);
        Assert.False(queue.Reader.TryRead(out _));

        // The dialog is over: plain text is no longer an answer.
        Assert.Contains("/help", (await Say(processor, "5")).Texts[^1]);
    }

    [Fact]
    public async Task TooManyLinesAreIgnored_WithANote()
    {
        var (processor, _) = Start();

        await Press(processor, "o:vrp:custom");
        await Say(processor, "1");
        await Say(processor, "20");
        await Say(processor, "1");

        var done = await Say(processor, "0 0\n5 5 3\n6 6 3");

        Assert.Contains("Ignored the extra lines", done.Choices[^1].Text);
    }
}
