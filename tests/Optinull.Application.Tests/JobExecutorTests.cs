using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests;

public sealed class JobExecutorTests
{
    private static OptimizationJob Enqueue(
        JobQueue queue,
        FakeReplies replies,
        string benchmark = "ft06",
        JobShopSolverKind solver = JobShopSolverKind.SimulatedAnnealing)
    {
        var result = queue.TryEnqueue(1, new SolveCommand(benchmark, solver), replies);

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

        var job = Enqueue(
            queue, replies, "ft10", JobShopSolverKind.GeneticAlgorithm);

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

        var job = Enqueue(
            queue, replies, "ft10", JobShopSolverKind.GeneticAlgorithm);

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
