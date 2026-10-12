using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests.Wizard;

public sealed class CustomDialogTests
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Chat
    {
        private readonly BotCommandProcessor _processor;
        private readonly long _id;

        public Chat(BotCommandProcessor processor, long id = 1)
        {
            _processor = processor;
            _id = id;
        }

        public async Task<FakeReplies> SayAsync(string text)
        {
            var replies = new FakeReplies();
            await _processor.ProcessAsync(text, _id, replies);
            return replies;
        }

        public async Task<FakeReplies> PressAsync(string data)
        {
            var replies = new FakeReplies();
            await _processor.ProcessCallbackAsync(data, _id, replies);
            return replies;
        }
    }

    private static (Chat Chat, JobQueue Queue, BotCommandProcessor Processor) Create(TimeProvider? time = null)
    {
        var queue = new JobQueue();
        var processor = new BotCommandProcessor(queue, null, time);

        return (new Chat(processor), queue, processor);
    }

    private static string Last(FakeReplies replies) => replies.Texts[^1];

    [Fact]
    public async Task JobShop_IsEnteredStepByStep_AndRuns()
    {
        var (chat, queue, _) = Create();

        var start = await chat.PressAsync("o:jobshop:custom");
        Assert.Contains("How many machines", start.Texts[0]);

        Assert.Contains("How many jobs", Last(await chat.SayAsync("2")));

        var firstJob = Last(await chat.SayAsync("2"));
        Assert.Contains("Job 1 of 2", firstJob);
        Assert.Contains("1-2", firstJob);

        var afterFirst = Last(await chat.SayAsync("1 4, 2 3"));
        Assert.Contains("Job 1: M1 (4) -> M2 (3)", afterFirst);
        Assert.Contains("Job 2 of 2", afterFirst);

        var done = await chat.SayAsync("2 5, 1 2");

        var choices = done.Choices[^1];
        Assert.Contains("Got it: custom job shop, 2 jobs x 2 machines", choices.Text);
        Assert.Equal(
            new[] { "greedy", "hc", "sa", "ga", "all" }.Select(key => $"o:jobshop:custom:{key}"),
            choices.Choices.Select(choice => choice.Data));

        var started = await chat.PressAsync("o:jobshop:custom:sa");

        Assert.Contains("Queued job #1", started.Texts[0]);
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal("custom", job.Instance.Name);

        // The job answers into the replies of the button press that started it.
        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal("custom-gantt.png", started.Pngs[0].FileName);
    }

    [Theory]
    [InlineData("1 4 2 3\n2 5 1 2")]
    [InlineData("1:4 2:3\n2:5 1:2")]
    [InlineData("1 4, 2 3\n\n2 5; 1 2")]
    public async Task JobShop_SeveralJobsInOneMessage_AreAllAccepted(string jobs)
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:jobshop:custom");
        await chat.SayAsync("2");
        await chat.SayAsync("2");

        var done = await chat.SayAsync(jobs);

        Assert.Contains("Got it", done.Choices[^1].Text);
    }

    [Fact]
    public async Task JobShop_MachinesAreCheckedAgainstTheAnswer_AndTheDialogStaysOnTheSameJob()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:jobshop:custom");
        await chat.SayAsync("2");
        await chat.SayAsync("1");

        var wrong = Last(await chat.SayAsync("1 4, 3 3"));

        Assert.Contains("machine 3 does not exist", wrong);
        Assert.Contains("1-2", wrong);
        Assert.Contains("Job 1 of 1", wrong);

        var fixedUp = await chat.SayAsync("1 4, 2 3");

        Assert.Contains("Got it", fixedUp.Choices[^1].Text);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("-1")]
    [InlineData("2.5")]
    public async Task Questions_AskAgain_WhenTheAnswerIsNotInRange(string answer)
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:jobshop:custom");

        var again = Last(await chat.SayAsync(answer));

        Assert.Contains("whole number from 1 to 20", again);
        Assert.Contains("How many machines", again);

        // The dialog is still on the first question.
        Assert.Contains("How many jobs", Last(await chat.SayAsync("3")));
    }

    [Fact]
    public async Task JobShop_BadLines_AreExplained_AndGoodLinesBeforeThemAreKept()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:jobshop:custom");
        await chat.SayAsync("2");
        await chat.SayAsync("3");

        var reply = Last(await chat.SayAsync("1 4, 2 3\n1 4 2\n2 1"));

        Assert.Contains("Job 1: M1 (4) -> M2 (3)", reply);
        Assert.Contains("That line was not used", reply);
        Assert.Contains("found 3 numbers", reply);
        Assert.Contains("Job 2 of 3", reply);
    }

    [Fact]
    public async Task Undo_RemovesTheLastEntry()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:knapsack:custom");
        await chat.SayAsync("50");
        await chat.SayAsync("3");
        await chat.SayAsync("60 10");

        var undone = Last(await chat.SayAsync("undo"));

        Assert.Contains("Removed the last one", undone);
        Assert.Contains("Item 1 of 3", undone);

        Assert.Contains("There is nothing to undo", Last(await chat.SayAsync("UNDO")));
    }

    [Fact]
    public async Task Tsp_CitiesCanArriveInSeveralMessages()
    {
        var (chat, queue, _) = Create();

        await chat.PressAsync("o:tsp:custom");

        Assert.Contains("Send the coordinates of the 5 cities", Last(await chat.SayAsync("5")));

        var partway = Last(await chat.SayAsync("0 0\n10,0"));

        Assert.Contains("2 of 5 cities so far", partway);
        Assert.Contains("Send 3 more", partway);

        var done = await chat.SayAsync("10 10\n0 10.5\n5 5");

        Assert.Contains("Got it: custom tour, 5 cities", done.Choices[^1].Text);

        await chat.PressAsync("o:tsp:custom:ga");
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.Equal(SolverKind.GeneticAlgorithm, job.Command.Solver);
    }

    [Theory]
    [InlineData("1 2 3", "found 3 values")]
    [InlineData("x y", "coordinates must be numbers")]
    [InlineData("5", "found 1 values")]
    [InlineData("2000000 1", "coordinates must be numbers")]
    public async Task Tsp_BadCities_AreExplained(string line, string reason)
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:tsp:custom");
        await chat.SayAsync("4");

        var reply = Last(await chat.SayAsync(line));

        Assert.Contains(reason, reply);
        Assert.Contains("That line was not used", reply);
    }

    [Fact]
    public async Task Knapsack_IsEnteredStepByStep_AndRunsToCompletion()
    {
        var (chat, queue, _) = Create();

        var start = await chat.PressAsync("o:knapsack:custom");
        Assert.Contains("capacity", start.Texts[0]);

        Assert.Contains("How many items", Last(await chat.SayAsync("50")));
        Assert.Contains("Item 1 of 3", Last(await chat.SayAsync("3")));

        var done = await chat.SayAsync("60 10\n100 20\n120 30");

        Assert.Contains("Item 3: value 120, weight 30", done.Choices[^1].Text);
        Assert.Contains("Got it: custom knapsack, 3 items, capacity 50", done.Choices[^1].Text);

        var started = await chat.PressAsync("o:knapsack:custom:all");
        Assert.True(queue.Reader.TryRead(out var job));
        Assert.True(job.Command.CompareAll);

        await new JobExecutor().ExecuteAsync(job);

        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Contains("Best:", started.Pngs[0].Caption);
        Assert.Contains(": 220 (", started.Pngs[0].Caption);
    }

    [Fact]
    public async Task Knapsack_WarnsAboutAnItemThatCanNeverFit()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:knapsack:custom");
        await chat.SayAsync("10");
        await chat.SayAsync("2");

        var done = await chat.SayAsync("5 3\n9 99");

        Assert.Contains("heavier than the knapsack", done.Choices[^1].Text);
    }

    [Fact]
    public async Task ExtraLines_AreIgnored_WithANote()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:knapsack:custom");
        await chat.SayAsync("50");
        await chat.SayAsync("1");

        var done = await chat.SayAsync("60 10\n70 20");

        Assert.Contains("Ignored the extra lines", done.Choices[^1].Text);
    }

    [Fact]
    public async Task Cancel_StopsTheDialog_AndLaterTextIsOrdinaryAgain()
    {
        var (chat, queue, _) = Create();

        await chat.PressAsync("o:tsp:custom");

        Assert.Contains("Cancelled", Last(await chat.SayAsync("/cancel")));
        Assert.Contains("/help", Last(await chat.SayAsync("5")));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task StartingSomethingElse_AbandonsTheDialog()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:tsp:custom");
        await chat.SayAsync("/optimize");

        Assert.Contains("/help", Last(await chat.SayAsync("5")));
    }

    [Fact]
    public async Task Help_DoesNotInterruptTheDialog()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:tsp:custom");
        await chat.SayAsync("/help");

        Assert.Contains("Send the coordinates of the 5 cities", Last(await chat.SayAsync("5")));
    }

    [Fact]
    public async Task Dialogs_BelongToTheirOwnChat()
    {
        var (chat, _, processor) = Create();
        var other = new Chat(processor, 2);

        await chat.PressAsync("o:tsp:custom");

        Assert.Contains("/help", Last(await other.SayAsync("5")));
        Assert.Contains("Send the coordinates", Last(await chat.SayAsync("5")));
    }

    [Fact]
    public async Task AnAbandonedDialog_IsForgottenAfterHalfAnHour()
    {
        var time = new ManualTime();
        var (chat, _, _) = Create(time);

        await chat.PressAsync("o:tsp:custom");

        time.Now = time.Now.AddMinutes(31);

        Assert.Contains("/help", Last(await chat.SayAsync("5")));
    }

    [Fact]
    public async Task AlgorithmButtons_NeedTheirOwnDialog()
    {
        var (chat, queue, _) = Create();

        // Never entered any data.
        Assert.Contains("out of date", Last(await chat.PressAsync("o:tsp:custom:sa")));

        // Data for another problem does not count.
        await chat.PressAsync("o:knapsack:custom");
        await chat.SayAsync("50");
        await chat.SayAsync("1");
        await chat.SayAsync("60 10");

        Assert.Contains("out of date", Last(await chat.PressAsync("o:tsp:custom:sa")));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task AnAlgorithmButton_WorksOnlyOnce()
    {
        var (chat, queue, _) = Create();

        await chat.PressAsync("o:knapsack:custom");
        await chat.SayAsync("50");
        await chat.SayAsync("1");
        await chat.SayAsync("60 10");

        Assert.Contains("Queued job", (await chat.PressAsync("o:knapsack:custom:sa")).Texts[0]);
        Assert.Contains("out of date", Last(await chat.PressAsync("o:knapsack:custom:sa")));

        Assert.True(queue.Reader.TryRead(out _));
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task NewDialog_ForgetsAReadyInstance()
    {
        var (chat, _, _) = Create();

        await chat.PressAsync("o:knapsack:custom");
        await chat.SayAsync("50");
        await chat.SayAsync("1");
        await chat.SayAsync("60 10");
        await chat.PressAsync("o:tsp:custom");

        Assert.Contains("out of date", Last(await chat.PressAsync("o:knapsack:custom:sa")));
    }
}
