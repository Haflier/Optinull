using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Problems;

namespace Optinull.Application.Tests;

public sealed class KnapsackCommandTests
{
    private static (BotCommandProcessor Processor, JobQueue Queue) Create()
    {
        var queue = new JobQueue();
        return (new BotCommandProcessor(queue), queue);
    }

    [Fact]
    public void Registry_KnowsTheKnapsackModuleAndItsBuiltIns()
    {
        var registry = ProblemRegistry.CreateDefault();

        Assert.NotNull(registry.FindModule("knapsack"));
        Assert.True(registry.TryGetBuiltIn("knap20", out _));
        Assert.True(registry.TryGetBuiltIn("KNAP100", out _));
    }

    [Theory]
    [InlineData("knap20")]
    [InlineData("knap100")]
    public async Task BuiltIn_IsQueued(string name)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync($"/solve {name} ga iterations=1000", 1, replies);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(name, job.Instance.Name);
        Assert.Equal(1000, job.Command.Iterations);
    }

    [Fact]
    public async Task RandomKnapsack_FromSettings_IsQueued()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve knapsack items=12 seed=5", 1, replies);

        Assert.Contains("12 items", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("knapsack12", job.Instance.Name);
    }

    [Fact]
    public async Task CustomKnapsack_IsQueuedWithItsItems()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve knapsack sa\n50\n60 10\n100 20\n120 30", 1, replies);

        Assert.Contains("3 items", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("custom", job.Instance.Name);
    }

    [Theory]
    [InlineData("/solve knapsack items=2", "items must be")]
    [InlineData("/solve knapsack cities=5", "Unknown parameter 'cities'")]
    public async Task BadSettings_AreRejected(string text, string reason)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync(text, 1, replies);

        Assert.Contains(reason, replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task InvalidCustomInstance_IsRejected_WithTheReason()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve knapsack\n50\n60", 1, replies);

        Assert.Contains("Invalid instance", replies.Texts[0]);
        Assert.Contains("Line 2", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("knap20")]
    [InlineData("knapsack")]
    public async Task Execute_SendsTwoCharts(string kind)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();
        var processor = new BotCommandProcessor(queue);

        var text = kind == "knapsack"
            ? "/solve knapsack ga iterations=500\n30\n10 5\n40 10\n30 15\n50 20"
            : "/solve knap20 sa iterations=1000";

        await processor.ProcessAsync(text, 1, replies);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(2, replies.Pngs.Count);
        Assert.EndsWith("-items.png", replies.Pngs[0].FileName);
        Assert.Contains("optimal", replies.Pngs[0].Caption);
        Assert.EndsWith("-convergence.png", replies.Pngs[1].FileName);
        Assert.All(replies.Pngs, png => Assert.True(png.Length > 1000));
    }
}
