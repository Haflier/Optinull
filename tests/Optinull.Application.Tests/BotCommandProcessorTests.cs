using Optinull.Application.Commands;

namespace Optinull.Application.Tests;

public sealed class BotCommandProcessorTests
{
    private sealed class FakeReplies : IBotReplies
    {
        public List<string> Texts { get; } = [];

        public List<(string FileName, string Caption, int Length)> Pngs { get; } = [];

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            Texts.Add(text);
            return Task.CompletedTask;
        }

        public Task SendPngAsync(
            byte[] png,
            string fileName,
            string caption,
            CancellationToken cancellationToken)
        {
            Pngs.Add((fileName, caption, png.Length));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Help_RepliesWithUsage()
    {
        var replies = new FakeReplies();

        await new BotCommandProcessor().ProcessAsync("/help", replies);

        Assert.Single(replies.Texts);
        Assert.Contains("/solve", replies.Texts[0]);
        Assert.Empty(replies.Pngs);
    }

    [Fact]
    public async Task UnknownBenchmark_ListsAvailableOnes()
    {
        var replies = new FakeReplies();

        await new BotCommandProcessor().ProcessAsync("/solve nope", replies);

        Assert.Single(replies.Texts);
        Assert.Contains("ft06", replies.Texts[0]);
        Assert.Empty(replies.Pngs);
    }

    [Fact]
    public async Task Solve_Ft06_SendsStatusThenTwoCharts()
    {
        var replies = new FakeReplies();

        await new BotCommandProcessor().ProcessAsync("/solve ft06", replies);

        Assert.Single(replies.Texts);
        Assert.Contains("ft06", replies.Texts[0]);

        Assert.Equal(2, replies.Pngs.Count);
        Assert.Equal("ft06-gantt.png", replies.Pngs[0].FileName);
        Assert.Contains("makespan", replies.Pngs[0].Caption);
        Assert.Equal("ft06-convergence.png", replies.Pngs[1].FileName);
        Assert.All(replies.Pngs, png => Assert.True(png.Length > 1000));
    }
}
