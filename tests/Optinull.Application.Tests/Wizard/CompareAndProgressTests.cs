using Optinull.Application.Commands;
using Optinull.Application.Comparison;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Tests.Wizard;

public sealed class CompareAndProgressTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47];

    private sealed class ListProgress : IProgress<double>
    {
        private readonly List<double> _values = [];

        public IReadOnlyList<double> Values
        {
            get { lock (_values) return _values.ToArray(); }
        }

        public void Report(double value)
        {
            lock (_values)
                _values.Add(value);
        }
    }

    private static IProblemInstance Instance(string name)
    {
        Assert.True(ProblemRegistry.CreateDefault().TryGetBuiltIn(name, out var instance));
        return instance!;
    }

    private static SolverOutcome Outcome(string name, double score) =>
        new(
            SolverKind.Greedy,
            name,
            score,
            TimeSpan.Zero,
            1,
            new ConvergenceSeries(name, [new ConvergencePointData(1, score)], 1),
            () => []);

    [Theory]
    [InlineData("knap20")]
    [InlineData("circle20")]
    [InlineData("ft06")]
    public void Compare_RunsEverySolver_AndReportsThemSideBySide(string name)
    {
        var report = new SolverComparisonService().Compare(
            Instance(name),
            new SolveOptions(Seed: 1, Iterations: 500));

        Assert.Equal(3, report.Images.Count);
        Assert.Equal($"{name}-compare.png", report.Images[0].FileName);
        Assert.Equal($"{name}-compare-convergence.png", report.Images[1].FileName);
        Assert.All(report.Images, image => Assert.Equal(PngSignature, image.Png.Take(4).ToArray()));

        var table = report.Images[0].Caption;

        foreach (var solver in new[] { "Greedy", "Hill Climbing", "Simulated Annealing", "Genetic Algorithm" })
            Assert.Contains(solver, table);

        Assert.Contains("Best:", table);
        Assert.Contains("Improvement over greedy", table);
    }

    [Fact]
    public void Compare_ProgressNeverGoesBackwards_AndEndsAtOne()
    {
        var progress = new ListProgress();

        new SolverComparisonService().Compare(
            Instance("knap20"),
            new SolveOptions(Seed: 1, Iterations: 1000, Progress: progress));

        var values = progress.Values;

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.InRange(value, 0.0, 1.0));
        Assert.Equal(values.OrderBy(value => value), values);
        Assert.Equal(1.0, values[^1], 6);
    }

    [Fact]
    public void Compare_IsReproducible_ForTheSameSeed()
    {
        var first = new SolverComparisonService().Compare(
            Instance("knap20"), new SolveOptions(Seed: 5, Iterations: 500));
        var second = new SolverComparisonService().Compare(
            Instance("knap20"), new SolveOptions(Seed: 5, Iterations: 500));

        // Times differ between runs, so compare the scores only.
        static IEnumerable<string> Scores(SolveReport report) =>
            report.Images[0].Caption.Split('\n')
                .Where(line => line.Contains(':'))
                .Select(line => line[..line.IndexOf('(')]);

        Assert.Equal(Scores(first).Skip(1).Take(4), Scores(second).Skip(1).Take(4));
    }

    [Fact]
    public void Compare_StopsWhenCancelled() =>
        Assert.Throws<OperationCanceledException>(() =>
            new SolverComparisonService().Compare(
                Instance("knap20"),
                new SolveOptions(),
                new CancellationToken(canceled: true)));

    [Fact]
    public void ConvergenceChart_IncludesGreedy_AsAFlatLineAsLongAsTheLongestRun()
    {
        var greedy = new SolverOutcome(
            SolverKind.Greedy, "Greedy", 260, TimeSpan.Zero, 1,
            new ConvergenceSeries("Greedy", [new ConvergencePointData(1, 260)], 1),
            () => []);

        var annealing = new SolverOutcome(
            SolverKind.SimulatedAnnealing, "Simulated Annealing", 190, TimeSpan.Zero, 5000,
            new ConvergenceSeries(
                "Simulated Annealing",
                [new ConvergencePointData(1, 300), new ConvergencePointData(900, 190)],
                5000),
            () => []);

        var series = SolverComparisonService.ConvergenceOf([greedy, annealing]);

        Assert.Equal(["Greedy", "Simulated Annealing"], series.Select(item => item.Name).ToArray());
        Assert.Equal(5000, series[0].FinalEvaluations);
        Assert.Equal(260, series[0].Points[0].Objective);
        Assert.Equal(new[] { 300.0, 190.0 }, series[1].Points.Select(point => point.Objective).ToArray());
        Assert.Equal(5000, series[1].FinalEvaluations);
    }

    [Theory]
    [InlineData("knap20")]
    [InlineData("ft06")]
    public void Compare_ConvergenceOfEverySolver_StillRendersWithGreedyIncluded(string name)
    {
        var report = new SolverComparisonService().Compare(
            Instance(name),
            new SolveOptions(Seed: 1, Iterations: 300));

        Assert.Contains("Greedy", report.Images[1].Caption);
        Assert.Equal(PngSignature, report.Images[1].Png.Take(4).ToArray());
    }

    [Fact]
    public void PickBest_RespectsTheDirection_AndPrefersTheEarlierOnATie()
    {
        SolverOutcome[] outcomes = [Outcome("a", 5), Outcome("b", 3), Outcome("c", 3), Outcome("d", 9)];

        Assert.Equal("b", SolverComparisonService.PickBest(outcomes, higherIsBetter: false).SolverName);
        Assert.Equal("d", SolverComparisonService.PickBest(outcomes, higherIsBetter: true).SolverName);
    }

    [Fact]
    public async Task Executor_RunsACompare_AndSendsThreeCharts_WithProgress()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync("/compare knap20 iterations=1000", 1, replies);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(3, replies.Pngs.Count);
        Assert.Equal("knap20-compare.png", replies.Pngs[0].FileName);
        Assert.Contains("Best:", replies.Pngs[0].Caption);

        Assert.NotEmpty(replies.Progress);
        Assert.Equal(1.0, replies.Progress[^1], 6);
    }

    [Fact]
    public async Task Executor_SingleRun_ReportsProgress_AndAClosingSummary()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync("/solve knap20 sa iterations=1000", 1, replies);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(2, replies.Pngs.Count);
        Assert.Equal(1.0, replies.Progress[^1], 6);

        var summary = replies.Texts[^1];

        Assert.Contains("Optimization completed", summary);
        Assert.Contains("Value:", summary);
        Assert.Contains("Improvement over greedy", summary);
    }

    [Theory]
    [InlineData("greedy")]
    [InlineData("hc")]
    public async Task Executor_FastSolvers_ShowNoProgressBar_AndNoImprovementOverThemselves(string solver)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync($"/solve circle20 {solver}", 1, replies);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Empty(replies.Progress);
        Assert.Equal(2, replies.Pngs.Count);

        if (solver == "greedy")
            Assert.DoesNotContain("Improvement", replies.Texts[^1]);
    }
}
