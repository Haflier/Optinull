using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Problems;

namespace Optinull.Application.Tests;

public sealed class BinPackingCommandTests
{
    private static (BotCommandProcessor Processor, JobQueue Queue) Create()
    {
        var queue = new JobQueue();
        return (new BotCommandProcessor(queue), queue);
    }

    [Fact]
    public void Registry_KnowsTheBinPackingModuleAndItsBuiltIns()
    {
        var registry = ProblemRegistry.CreateDefault();

        Assert.NotNull(registry.FindModule("binpacking"));
        Assert.True(registry.TryGetBuiltIn("bin30", out _));
        Assert.True(registry.TryGetBuiltIn("BIN120", out _));
    }

    [Theory]
    [InlineData("bin30")]
    [InlineData("bin120")]
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
    public async Task RandomPacking_FromSettings_IsQueued()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve binpacking items=12 capacity=50 minsize=5 maxsize=30 seed=5", 1, replies);

        Assert.Contains("12 items", replies.Texts[0]);
        Assert.Contains("capacity 50", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("binpacking12", job.Instance.Name);
    }

    [Fact]
    public async Task SizesLeftOut_FollowTheCapacity()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve binpacking items=10 capacity=20", 1, replies);

        Assert.Contains("size 2-10", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task CustomPacking_IsQueuedWithItsItems()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/solve binpacking sa\n100\n60 2\n40 1", 1, replies);

        Assert.Contains("3 items", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("custom", job.Instance.Name);
    }

    [Theory]
    [InlineData("/solve binpacking items=2", "items must be")]
    [InlineData("/solve binpacking cities=5", "Unknown parameter 'cities'")]
    [InlineData("/solve binpacking maxsize=150", "maxsize cannot be more than the capacity")]
    [InlineData("/solve binpacking minsize=60", "minsize (60) cannot be more than maxsize (50)")]
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

        await processor.ProcessAsync("/solve binpacking\n50\n60", 1, replies);

        Assert.Contains("Invalid instance", replies.Texts[0]);
        Assert.Contains("Line 2", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData("bin30")]
    [InlineData("binpacking")]
    public async Task Execute_SendsTwoCharts(string kind)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();
        var processor = new BotCommandProcessor(queue);

        var text = kind == "binpacking"
            ? "/solve binpacking ga iterations=500\n100\n60 2\n40 1\n50 2\n30 1\n70 1"
            : "/solve bin30 sa iterations=1000";

        await processor.ProcessAsync(text, 1, replies);
        Assert.True(queue.Reader.TryRead(out var job));

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(2, replies.Pngs.Count);
        Assert.EndsWith("-bins.png", replies.Pngs[0].FileName);
        Assert.Contains("lower bound", replies.Pngs[0].Caption);
        Assert.EndsWith("-convergence.png", replies.Pngs[1].FileName);
        Assert.All(replies.Pngs, png => Assert.True(png.Length > 1000));
    }

    [Fact]
    public void Dialog_CollectsCapacityThenSizesAndQuantities()
    {
        var dialog = new BinPackingModule().CreateDialog();

        Assert.Contains("How much can one bin hold", dialog.Start());
        Assert.IsType<DialogAsk>(dialog.Answer("100"));
        Assert.IsType<DialogAsk>(dialog.Answer("2"));

        var done = Assert.IsType<DialogDone>(dialog.Answer("60 2\n40"));

        Assert.Contains("3 items", done.Instance.Description);
    }

    [Fact]
    public void Dialog_RejectsASizeThatCannotFit_AndAnOverlargeTotal()
    {
        var dialog = new BinPackingModule().CreateDialog();

        dialog.Start();
        dialog.Answer("100");
        dialog.Answer("3");

        var tooBig = Assert.IsType<DialogAsk>(dialog.Answer("150 1"));
        Assert.Contains("bin capacity 100", tooBig.Text);

        dialog.Answer("10 150");

        var tooMany = Assert.IsType<DialogAsk>(dialog.Answer("10 100"));
        Assert.Contains("the limit is 200", tooMany.Text);
    }

    [Fact]
    public void Dialog_Undo_RemovesTheLastLine()
    {
        var dialog = new BinPackingModule().CreateDialog();

        dialog.Start();
        dialog.Answer("100");
        dialog.Answer("2");

        Assert.Contains("nothing to undo", Assert.IsType<DialogAsk>(dialog.Answer("undo")).Text);

        dialog.Answer("60 2");

        Assert.Contains("Removed the last one", Assert.IsType<DialogAsk>(dialog.Answer("undo")).Text);
    }
}
