using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests.Tuning;

public sealed class TuningCommandTests
{
    private static async Task<(OptimizationJob Job, FakeReplies Replies)> QueueAsync(string text)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync(text, 1, replies);

        Assert.True(queue.Reader.TryRead(out var job), $"Nothing was queued: {string.Join(" | ", replies.Texts)}");

        return (job, replies);
    }

    [Fact]
    public async Task ASingleRun_ReportsEverySettingItUsed_MarkingWhoChoseIt()
    {
        var (job, replies) = await QueueAsync("/solve knap20 sa iterations=1000 temperature=2 final=0.001");

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);

        var summary = replies.Texts[^1];

        Assert.Contains("temperature=2 (yours)", summary);
        Assert.Contains("final=0.001 (yours)", summary);
        Assert.Contains("moves=5 (auto)", summary);
    }

    [Fact]
    public async Task ARunWithoutSettings_StillShowsWhatTheEngineChose()
    {
        var (job, replies) = await QueueAsync("/solve knap20 ga iterations=2000");

        await new JobExecutor().ExecuteAsync(job);

        var summary = replies.Texts[^1];

        Assert.Contains("Settings: population=", summary);
        Assert.Contains("(auto)", summary);
        Assert.DoesNotContain("(yours)", summary);
    }

    [Fact]
    public async Task GreedyAndHillClimbing_HaveNoSettingsToShow()
    {
        var (job, replies) = await QueueAsync("/solve knap20 greedy");

        await new JobExecutor().ExecuteAsync(job);

        Assert.DoesNotContain("Settings:", replies.Texts[^1]);
    }

    [Fact]
    public async Task ACompare_ShowsWhichSolverGotWhichSetting()
    {
        var (job, replies) = await QueueAsync("/compare knap20 iterations=500 mutation=0.3 temperature=2");

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Contains("Settings: temperature=2 (SA), mutation=0.3 (GA)", replies.Pngs[0].Caption);
    }

    [Fact]
    public async Task TuningWorksWithAGeneratedInstance_AndAFixedOne()
    {
        var (generated, _) = await QueueAsync("/solve tsp cities=15 ga population=20 mutation=0.4 iterations=500");
        var (fixedOne, _) = await QueueAsync("/solve ft06 ils perturbation=5 iterations=500");

        Assert.Equal(20, generated.Command.Tuning!.Population);
        Assert.Equal(5, fixedOne.Command.Tuning!.Perturbation);
        Assert.Equal("15", generated.Command.ParameterValues["cities"]);
    }

    [Fact]
    public async Task SettingsThatCannotWorkTogether_StopTheJobWithAnExplanation()
    {
        var (job, replies) = await QueueAsync("/solve knap20 ga iterations=500 population=5 elite=9");

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("elite (9) cannot be more than population (5)", replies.Texts[^1]);
        Assert.DoesNotContain("Please try again", replies.Texts[^1]);
    }

    [Theory]
    [InlineData("/solve ft06 population=10", "add ga to the command")]
    [InlineData("/solve ft06 ga temperature=2", "temperature tunes Simulated Annealing")]
    [InlineData("/solve ft06 sa temperature=0", "temperature must be a number from 0.01 to 100")]
    [InlineData("/compare ft06 mutation=2", "mutation must be a number from 0 to 1")]
    public async Task BadSettings_AreAnsweredInTheChat_AndNothingIsQueued(string text, string reason)
    {
        var queue = new JobQueue();
        var replies = new FakeReplies();

        await new BotCommandProcessor(queue).ProcessAsync(text, 1, replies);

        Assert.Contains(reason, Assert.Single(replies.Texts));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Help_ListsEverySetting_AndStillFitsInOneTelegramMessage()
    {
        var replies = new FakeReplies();

        await new BotCommandProcessor(new JobQueue()).ProcessAsync("/help", 1, replies);

        var help = replies.Texts[0];

        foreach (var key in TuningCatalog.Keys)
            Assert.Contains($"{key.Name}={TuningCatalog.Range(key)}", help);

        Assert.Contains("sa temperature=0.01-100", help);
        Assert.True(help.Length < 4096, $"The help text is {help.Length} characters; Telegram allows 4096.");
    }

    [Fact]
    public void TheUsageHint_MentionsTuning() =>
        Assert.Contains("tuning", BotCommandParser.Usage);
}
