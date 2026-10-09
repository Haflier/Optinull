using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests;

public sealed class JobQueueTests
{
    private static readonly SolveCommand Command =
        new("ft06", JobShopSolverKind.SimulatedAnnealing);

    [Fact]
    public void FinishedJob_DoesNotBlockTheChat()
    {
        var queue = new JobQueue();

        var first = Assert.IsType<Enqueued>(
            queue.TryEnqueue(1, Command, new FakeReplies())).Job;

        Assert.True(first.TryStart());
        first.Complete();

        Assert.IsType<Enqueued>(queue.TryEnqueue(1, Command, new FakeReplies()));
    }

    [Fact]
    public void Release_IsSafeToCallTwice()
    {
        var queue = new JobQueue();

        var job = Assert.IsType<Enqueued>(
            queue.TryEnqueue(1, Command, new FakeReplies())).Job;

        queue.Release(job);
        queue.Release(job);

        Assert.IsType<Enqueued>(queue.TryEnqueue(1, Command, new FakeReplies()));
    }

    [Fact]
    public void Cancel_FinishedJob_ReturnsNull()
    {
        var queue = new JobQueue();

        var job = Assert.IsType<Enqueued>(
            queue.TryEnqueue(1, Command, new FakeReplies())).Job;

        job.TryStart();
        job.Complete();

        Assert.Null(queue.Cancel(1));
    }

    [Fact]
    public void Constructor_NonPositiveCapacity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobQueue(0));
}
