using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Application.Tests;

public sealed class BotCommandProcessorTests
{
    private const string SmallJobShop = "2 2\n0 3 1 2\n1 4 0 2";

    private static (BotCommandProcessor Processor, JobQueue Queue) Create(int capacity = 10)
    {
        var queue = new JobQueue(capacity);
        return (new BotCommandProcessor(queue), queue);
    }

    [Fact]
    public async Task Help_ListsInstancesProblemsAndCancel()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/help", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("/solve", replies.Texts[0]);
        Assert.Contains("/cancel", replies.Texts[0]);
        Assert.Contains("jobshop", replies.Texts[0]);
        Assert.Contains("tsp", replies.Texts[0]);
        Assert.Contains("circle20", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task UnknownInstance_ListsWhatIsAvailable_AndDoesNotQueue()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve nope", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("ft06", replies.Texts[0]);
        Assert.Contains("circle20", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("ft06")]
    [InlineData("circle20")]
    [InlineData("grid8")]
    [InlineData("random50")]
    public async Task BuiltIn_IsQueuedWithItsInstance(string name)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync($"/solve {name}", 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Empty(replies.Pngs);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(name, job.Instance.Name);
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

    [Fact]
    public async Task CustomJobShop_IsQueuedWithItsProblem()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve jobshop\n" + SmallJobShop, 1, replies);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Contains("2 jobs x 2 machines", replies.Texts[0]);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("custom", job.Instance.Name);
        Assert.Null(job.Command.InstanceText);
    }

    [Fact]
    public async Task CustomTsp_IsQueuedWithItsCities()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve tsp ga\n0 0\n10 0\n10 10\n0 10", 1, replies);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Contains("4 cities", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("custom", job.Instance.Name);
    }

    [Theory]
    [InlineData("/solve jobshop\n2 2\n0 3 1 2", "Expected 2 job lines")]
    [InlineData("/solve tsp\n0 0\n1 1", "at least 3 cities")]
    public async Task InvalidCustomInstance_IsRejectedImmediately_WithTheReason(
        string text,
        string reason)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync(text, 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("Invalid instance", replies.Texts[0]);
        Assert.Contains(reason, replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("/solve jobshop")]
    [InlineData("/solve tsp ga")]
    public async Task CustomWithoutInstance_ExplainsTheFormat(string text)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync(text, 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains("lines after the command", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task OversizedJobShop_IsRejected()
    {
        var jobs = JobShopInputLimits.Default.MaxJobs + 1;
        var lines = string.Join("\n", Enumerable.Repeat("0 1", jobs));

        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync($"/solve jobshop\n{jobs} 1\n{lines}", 1, replies);

        Assert.Contains("Too many jobs", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task RandomTsp_FromSettings_IsQueuedWithOptions()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve tsp ga iterations=500 cities=12 seed=3", 1, replies);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.Contains("12 cities", replies.Texts[0]);
        Assert.Contains("500 iterations", replies.Texts[0]);

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("tsp12", job.Instance.Name);
        Assert.Equal(500, job.Command.Iterations);
        Assert.Equal(3, job.Command.Seed);
    }

    [Fact]
    public async Task RandomJobShop_UsesDefaultsForMissingSettings()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve jobshop machines=4", 1, replies);

        Assert.Contains("6 jobs x 4 machines", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("/solve tsp cities=2", "cities must be")]
    [InlineData("/solve tsp cities=many", "cities must be")]
    [InlineData("/solve tsp jobs=3", "Unknown parameter 'jobs'")]
    [InlineData("/solve jobshop jobs=20 machines=21", "machines must be")]
    public async Task BadSettings_AreRejected_WithTheReason(string text, string reason)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync(text, 1, replies);

        Assert.Single(replies.Texts);
        Assert.Contains(reason, replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task SettingsOnAFixedInstance_AreRejected()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve ft06 cities=5", 1, replies);

        Assert.Contains("takes no settings", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task InstanceLinesTogetherWithSettings_AreRejected()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve tsp cities=5\n0 0\n1 0\n1 1", 1, replies);

        Assert.Contains("not both", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task IterationsOnABuiltIn_TravelWithTheJob()
    {
        var (processor, queue) = Create();

        await processor.ProcessAsync("/solve ft06 ga iterations=2000", 1, new FakeReplies());

        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(2000, job.Command.Iterations);
    }
}
