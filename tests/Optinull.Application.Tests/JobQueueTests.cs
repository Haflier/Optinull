using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;

namespace Optinull.Application.Tests;

public sealed class JobQueueTests
{
    private static readonly SolveCommand Command =
        new("ft06", SolverKind.SimulatedAnnealing);

    private static IProblemInstance Instance()
    {
        Assert.True(ProblemRegistry.CreateDefault().TryGetBuiltIn("ft06", out var instance));
        return instance!;
    }

    [Fact]
    public void FinishedJob_DoesNotBlockTheChat()
    {
        var queue = new JobQueue();

        var first = Assert.IsType<Enqueued>(
            queue.TryEnqueue(1, Command, new FakeReplies(), Instance())).Job;

        Assert.True(first.TryStart());
        first.Complete();

        Assert.IsType<Enqueued>(queue.TryEnqueue(1, Command, new FakeReplies(), Instance()));
    }

    [Fact]
    public void Release_IsSafeToCallTwice()
    {
        var queue = new JobQueue();

        var job = Assert.IsType<Enqueued>(
            queue.TryEnqueue(1, Command, new FakeReplies(), Instance())).Job;

        queue.Release(job);
        queue.Release(job);

        Assert.IsType<Enqueued>(queue.TryEnqueue(1, Command, new FakeReplies(), Instance()));
    }

    [Fact]
    public void Cancel_FinishedJob_ReturnsNull()
    {
        var queue = new JobQueue();

        var job = Assert.IsType<Enqueued>(
            queue.TryEnqueue(1, Command, new FakeReplies(), Instance())).Job;

        job.TryStart();
        job.Complete();

        Assert.Null(queue.Cancel(1));
    }

    [Fact]
    public void Constructor_NonPositiveCapacity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobQueue(0));
}
