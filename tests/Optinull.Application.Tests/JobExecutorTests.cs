using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;

namespace Optinull.Application.Tests;

public sealed class JobExecutorTests
{
    private static OptimizationJob Enqueue(
        JobQueue queue,
        FakeReplies replies,
        string instanceName = "ft06",
        SolverKind solver = SolverKind.SimulatedAnnealing)
    {
        Assert.True(ProblemRegistry.CreateDefault().TryGetBuiltIn(instanceName, out var instance));

        var result = queue.TryEnqueue(
            1,
            new SolveCommand(instanceName, solver),
            replies,
            instance!);

        return Assert.IsType<Enqueued>(result).Job;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 500; i++)
        {
            if (condition())
                return;

            await Task.Delay(10);
        }

        Assert.Fail("Condition was not met within 5 seconds.");
    }

    [Fact]
    public async Task Execute_Ft06_CompletesAndSendsTwoCharts()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();
        var job = Enqueue(queue, replies);

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Null(job.Error);

        Assert.Contains("Solving job #1", replies.Texts[0]);
        Assert.Equal(2, replies.Pngs.Count);
        Assert.Equal("ft06-gantt.png", replies.Pngs[0].FileName);
        Assert.Contains("makespan", replies.Pngs[0].Caption);
        Assert.Equal("ft06-convergence.png", replies.Pngs[1].FileName);
        Assert.All(replies.Pngs, png => Assert.True(png.Length > 1000));
    }

    [Theory]
    [InlineData(SolverKind.SimulatedAnnealing)]
    [InlineData(SolverKind.GeneticAlgorithm)]
    public async Task Execute_TspCircle_SendsRouteAndConvergence(SolverKind solver)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();
        var job = Enqueue(queue, replies, "circle20", solver);

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(2, replies.Pngs.Count);
        Assert.Equal("circle20-route.png", replies.Pngs[0].FileName);
        Assert.Contains("optimal", replies.Pngs[0].Caption);
        Assert.Equal("circle20-convergence.png", replies.Pngs[1].FileName);
    }

    [Fact]
    public async Task Execute_CustomJobShop_UsesALowerBoundCaption()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue)
            .ProcessAsync("/solve jobshop\n2 2\n0 3 1 2\n1 4 0 2", 1, replies);

        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("custom-gantt.png", replies.Pngs[0].FileName);
        Assert.Contains("lower bound 6", replies.Pngs[0].Caption);
    }

    [Fact]
    public async Task Execute_CustomTsp_UsesANearestNeighbourCaption()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue)
            .ProcessAsync("/solve tsp\n0 0\n10 0\n10 10\n0 10\n5 5", 1, replies);

        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("custom-route.png", replies.Pngs[0].FileName);
        Assert.Contains("nearest neighbour", replies.Pngs[0].Caption);
    }

    [Fact]
    public async Task Execute_JobCancelledWhileQueued_IsSkipped()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();
        var job = Enqueue(queue, replies);

        queue.Cancel(1);

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Empty(replies.Texts);
        Assert.Empty(replies.Pngs);
    }

    [Fact]
    public async Task Execute_CancelledWhileRunning_StopsWithoutCharts()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        var job = Enqueue(queue, replies, "ft10", SolverKind.GeneticAlgorithm);

        var running = Task.Run(() => new JobExecutor().ExecuteAsync(job));

        await WaitUntilAsync(() => job.Status == JobStatus.Running);

        var result = queue.Cancel(1);

        await running.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.NotNull(result);
        Assert.True(result.WasRunning);
        Assert.Equal(JobStatus.Cancelled, job.Status);
        Assert.Empty(replies.Pngs);
        Assert.Contains(replies.Texts, text => text.Contains("cancelled"));
    }

    [Fact]
    public async Task Execute_ExceedingTimeLimit_FailsWithoutCharts()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        var job = Enqueue(queue, replies, "ft10", SolverKind.GeneticAlgorithm);

        await new JobExecutor(TimeSpan.FromMilliseconds(50))
            .ExecuteAsync(job)
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("Timed out", job.Error);
        Assert.Empty(replies.Pngs);
        Assert.Contains(replies.Texts, text => text.Contains("longer than"));
    }

    [Fact]
    public void Constructor_NonPositiveTimeLimit_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new JobExecutor(TimeSpan.Zero));
}
