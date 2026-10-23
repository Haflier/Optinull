using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;

namespace Optinull.Application.Tests.Wizard;

public sealed class OptimizeWizardTests
{
    private static (BotCommandProcessor Processor, JobQueue Queue) Create(int capacity = 100)
    {
        var queue = new JobQueue(capacity);
        return (new BotCommandProcessor(queue), queue);
    }

    private static string[] Data(FakeReplies replies) =>
        replies.Choices[^1].Choices.Select(choice => choice.Data).ToArray();

    [Fact]
    public async Task Optimize_OffersOneButtonPerProblem()
    {
        var (processor, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/optimize", 1, replies);

        Assert.Equal("Choose a problem:", replies.Choices[^1].Text);
        Assert.Equal(new[] { "o:jobshop", "o:tsp", "o:knapsack", "o:vrp", "o:binpacking" }, Data(replies));
    }

    [Fact]
    public async Task ChoosingAProblem_OffersTheDatasetsAndCustom()
    {
        var (processor, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:tsp", 1, replies);

        Assert.Contains("Choose dataset", replies.Choices[^1].Text);
        Assert.Equal(
            new[] { "o:tsp:small", "o:tsp:medium", "o:tsp:large", "o:tsp:custom" },
            Data(replies));
    }

    [Fact]
    public async Task ChoosingADataset_OffersAllFiveAlgorithms()
    {
        var (processor, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:jobshop:medium", 1, replies);

        Assert.Contains("Choose algorithm", replies.Choices[^1].Text);
        Assert.Equal(
            new[]
            {
                "o:jobshop:medium:greedy",
                "o:jobshop:medium:hc",
                "o:jobshop:medium:ils",
                "o:jobshop:medium:sa",
                "o:jobshop:medium:ga",
                "o:jobshop:medium:all"
            },
            Data(replies));
        Assert.Equal(
            new[] { "Greedy", "Hill Climbing", "Iterated Local Search", "Simulated Annealing", "Genetic Algorithm", "Compare All" },
            replies.Choices[^1].Choices.Select(choice => choice.Label).ToArray());
    }

    [Fact]
    public async Task Custom_StartsAGuidedDialog_WithTheFirstQuestion()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:tsp:custom", 1, replies);

        Assert.StartsWith("Problem:", replies.Choices[^1].Text);
        Assert.Empty(replies.Choices[^1].Choices);
        Assert.Contains("How many cities", replies.Texts[0]);
        Assert.Contains("/cancel", replies.Texts[0]);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task LastChoice_ReplacesTheButtons_AndStartsTheJob()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:tsp:small:ga", 1, replies);

        var summary = replies.Choices[^1];

        Assert.StartsWith("Problem:", summary.Text);
        Assert.Contains("Genetic Algorithm", summary.Text);
        Assert.Empty(summary.Choices);

        Assert.Contains("Queued job #1", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(SolverKind.GeneticAlgorithm, job.Command.Solver);
        Assert.False(job.Command.CompareAll);
        Assert.Equal("tsp15", job.Instance.Name);
    }

    [Fact]
    public async Task CompareAll_StartsAComparison()
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync("o:knapsack:large:all", 1, replies);

        Assert.Contains("all solvers", replies.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.True(job.Command.CompareAll);
        Assert.Equal("knap100", job.Instance.Name);
    }

    [Theory]
    [InlineData("o:nope")]
    [InlineData("o:tsp:nope")]
    [InlineData("o:tsp:small:nope")]
    [InlineData("o:nope:small:ga")]
    public async Task OutOfDateButtons_AskToStartAgain(string data)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync(data, 1, replies);

        Assert.Contains("out of date", Assert.Single(replies.Texts));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("o")]
    [InlineData("o:tsp:small:ga:extra")]
    public async Task UnrelatedCallbackData_IsIgnored(string? data)
    {
        var (processor, queue) = Create();
        var replies = new FakeReplies();

        await processor.ProcessCallbackAsync(data, 1, replies);

        Assert.Empty(replies.Texts);
        Assert.Empty(replies.Choices);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task EveryPresetWithEveryAlgorithm_ResolvesToARunnableJob()
    {
        var (processor, queue) = Create();
        var registry = ProblemRegistry.CreateDefault();
        var chat = 0L;
        var started = 0;

        foreach (var module in registry.Modules)
        {
            Assert.Equal(3, module.Presets.Count);

            foreach (var preset in module.Presets)
            {
                foreach (var algorithm in new[] { "greedy", "hc", "ils", "sa", "ga", "all" })
                {
                    var replies = new FakeReplies();

                    await processor.ProcessCallbackAsync(
                        $"o:{module.Id}:{preset.Key}:{algorithm}",
                        ++chat,
                        replies);

                    Assert.Contains(replies.Texts, text => text.StartsWith("Queued job"));
                    started++;
                }
            }
        }

        var jobs = 0;

        while (queue.Reader.TryRead(out _))
            jobs++;

        Assert.Equal(started, jobs);
        Assert.Equal(registry.Modules.Count * 3 * 6, jobs);
    }

    [Fact]
    public async Task Help_MentionsTheWizardAndCompare()
    {
        var (processor, _) = Create();
        var replies = new FakeReplies();

        await processor.ProcessAsync("/help", 1, replies);

        Assert.Contains("/optimize", replies.Texts[0]);
        Assert.Contains("/compare", replies.Texts[0]);
        Assert.Contains("hc", replies.Texts[0]);
    }
}
