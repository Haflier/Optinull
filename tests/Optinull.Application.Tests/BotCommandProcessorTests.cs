using Optinull.Application.Commands;
using Optinull.Application.Jobs;

namespace Optinull.Application.Tests;

public sealed class BotCommandProcessorTests
{
    private static (BotCommandProcessor Processor, JobQueue Queue) Create(int capacity = 10)
    {
        var queue = new JobQueue(capacity);
        return (new BotCommandProcessor(queue), queue);
    }

    [Fact]
    public async Task Help_RepliesWithUsage()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/help", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("/solve", replies.Texts[0]);
        Assert.Contains("/cancel", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task UnknownBenchmark_ListsAvailableOnes_AndDoesNotQueue()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve nope", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("ft06", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Solve_QueuesJob_AndRepliesWithItsId()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve ft06", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Empty(replies.Pngs);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal("ft06", job.Command.Benchmark);
    }

    [Fact]
    public async Task SecondSolveFromSameChat_IsRejected()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve ft06", 1, replies);
        await processor.ProcessAsync("/solve ft10", 1, replies);

        Assert.Contains("already have job #1", replies.Texts[1]);

        Assert.True(queue.Reader.TryRead(out _));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task SolveFromAnotherChat_IsAccepted()
    {
        var (processor, queue) = Create();

        await processor.ProcessAsync("/solve ft06", 1, new FakeReplies());

        var second = new FakeReplies();
        await processor.ProcessAsync("/solve ft06", 2, second);

        Assert.Contains("Queued job #2", second.Texts[0]);
        Assert.True(queue.Reader.TryRead(out _));
        Assert.True(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task FullQueue_RejectsNewJobs()
    {
        var (processor, _) = Create(capacity: 1);

        await processor.ProcessAsync("/solve ft06", 1, new FakeReplies());

        var second = new FakeReplies();
        await processor.ProcessAsync("/solve ft06", 2, second);

        Assert.Contains("queue is full", second.Texts[0]);
    }

    [Fact]
    public async Task Cancel_WithoutJob_SaysSo()
    {
        var (processor, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/cancel", 1, replies);

        Assert.Contains("no job", replies.Texts[0]);
    }

    [Fact]
    public async Task Cancel_QueuedJob_CancelsIt_AndFreesTheChat()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve ft06", 1, replies);
        await processor.ProcessAsync("/cancel", 1, replies);

        Assert.Contains("cancelled", replies.Texts[1]);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(JobStatus.Cancelled, job.Status);

        await processor.ProcessAsync("/solve ft06", 1, replies);
        Assert.Contains("Queued job #2", replies.Texts[2]);
    }
}
