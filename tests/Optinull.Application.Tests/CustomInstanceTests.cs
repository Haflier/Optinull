using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Application.Tests;

public sealed class CustomInstanceTests
{
    private const string Small = "2 2\n0 3 1 2\n1 4 0 2";

    [Theory]
    [InlineData("/solve custom\n" + Small)]
    [InlineData("/solve custom ga\r\n2 2\r\n0 3 1 2\r\n1 4 0 2")]
    [InlineData("/solve@OptinullBot custom\n" + Small)]
    public void CustomWithInstance_ParsesToSolveCommandWithText(string text)
    {
        var command = Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

        Assert.Equal("custom", command.Benchmark);
        Assert.Contains("0 3 1 2", command.InstanceText);
    }

    [Fact]
    public void CustomWithGa_UsesGeneticAlgorithm()
    {
        var command = Assert.IsType<SolveCommand>(
            BotCommandParser.Parse("/solve custom ga\n" + Small));

        Assert.Equal(JobShopSolverKind.GeneticAlgorithm, command.Solver);
    }

    [Theory]
    [InlineData("/solve custom")]
    [InlineData("/solve custom ga\n   \n")]
    public void CustomWithoutInstance_IsInvalid(string text) =>
        Assert.IsType<InvalidCommand>(BotCommandParser.Parse(text));

    [Fact]
    public void BuiltInBenchmark_IgnoresExtraLines()
    {
        var command = Assert.IsType<SolveCommand>(
            BotCommandParser.Parse("/solve ft06\nsome text"));

        Assert.Equal("ft06", command.Benchmark);
        Assert.Null(command.InstanceText);
    }

    [Fact]
    public async Task ValidCustomInstance_IsQueuedWithItsProblem()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue)
            .ProcessAsync("/solve custom\n" + Small, 1, replies);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Contains("2 jobs x 2 machines", replies.Texts[0]);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.NotNull(job.CustomProblem);
        Assert.Equal(2, job.CustomProblem.Jobs.Count);
        Assert.Null(job.Command.InstanceText);
    }

    [Fact]
    public async Task InvalidCustomInstance_IsRejectedImmediately_WithTheReason()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue)
            .ProcessAsync("/solve custom\n2 2\n0 3 1 2", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("Invalid instance", replies.Texts[0]);
        Assert.Contains("Expected 2 job lines", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task OversizedCustomInstance_IsRejected()
    {
        var limits = JobShopInputLimits.Default;

        var tooManyJobs = limits.MaxJobs + 1;
        var lines = Enumerable.Repeat("0 1", tooManyJobs);
        var text = $"/solve custom\n{tooManyJobs} 1\n{string.Join("\n", lines)}";

        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync(text, 1, replies);

        Assert.Contains("Too many jobs", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Execute_CustomJob_SendsChartsWithLowerBoundCaption()
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue)
            .ProcessAsync("/solve custom\n" + Small, 1, replies);

        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(2, replies.Pngs.Count);
        Assert.Equal("custom-gantt.png", replies.Pngs[0].FileName);
        Assert.Contains("lower bound 6", replies.Pngs[0].Caption);
    }
}
