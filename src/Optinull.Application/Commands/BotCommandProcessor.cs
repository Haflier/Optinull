using Optinull.Application.Jobs;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;
using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Application.Commands;

/// <summary>Understands chat commands; solving itself happens on the job queue.</summary>
public sealed class BotCommandProcessor
{
    private readonly JobQueue _queue;

    public BotCommandProcessor(JobQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
    }

    public static string HelpText
    {
        get
        {
            var limits = JobShopInputLimits.Default;

            return
                "I solve job shop scheduling problems and send back a Gantt chart " +
                "and a convergence chart.\n\n" +
                "/solve <benchmark> [sa|ga]\n" +
                $"Benchmarks: {string.Join(", ", JobShopBenchmarkCatalog.Names)}\n" +
                "sa = simulated annealing (default), ga = genetic algorithm\n\n" +
                "Your own problem: send /solve custom [sa|ga] with the instance on the " +
                "next lines, or attach a .txt file with /solve custom as the caption.\n" +
                "First line: jobs and machines. Then one line per job with " +
                "'machine time' pairs, machines numbered from 0. Example:\n" +
                "/solve custom\n3 3\n0 3 1 2 2 2\n0 2 2 1 1 4\n1 4 2 3 0 1\n" +
                $"Limits: {limits.MaxJobs} jobs, {limits.MaxMachines} machines, " +
                $"{limits.MaxOperations} operations.\n\n" +
                "/cancel - stop your current job";
        }
    }

    public async Task ProcessAsync(
        string text,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replies);

        switch (BotCommandParser.Parse(text))
        {
            case HelpCommand:
                await replies.SendTextAsync(HelpText, cancellationToken);
                break;

            case InvalidCommand invalid:
                await replies.SendTextAsync(invalid.Message, cancellationToken);
                break;

            case SolveCommand solve:
                await EnqueueAsync(solve, chatId, replies, cancellationToken);
                break;

            case CancelCommand:
                await CancelAsync(chatId, replies, cancellationToken);
                break;
        }
    }

    private async Task EnqueueAsync(
        SolveCommand command,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        JobShopProblem? custom = null;
        var queued = command;
        var label = command.Benchmark;

        if (command.Benchmark == BotCommandParser.CustomBenchmark)
        {
            if (!JobShopTextParser.TryParse(
                    command.InstanceText,
                    JobShopInputLimits.Default,
                    out custom,
                    out var error))
            {
                await replies.SendTextAsync(
                    $"Invalid instance: {error}\n\nSend /help to see the format.",
                    cancellationToken);
                return;
            }

            // The parsed problem travels with the job; drop the raw text.
            queued = command with { InstanceText = null };
            label = $"custom, {custom!.Jobs.Count} jobs x {custom.MachineCount} machines";
        }
        else if (!JobShopBenchmarkCatalog.TryGet(command.Benchmark, out _))
        {
            await replies.SendTextAsync(
                $"Unknown benchmark '{command.Benchmark}'. " +
                $"Available: {string.Join(", ", JobShopBenchmarkCatalog.Names)}, " +
                $"or {BotCommandParser.CustomBenchmark} for your own problem.",
                cancellationToken);
            return;
        }

        var message = _queue.TryEnqueue(chatId, queued, replies, custom) switch
        {
            Enqueued enqueued =>
                $"Queued job #{enqueued.Job.Id} ({label}, {command.Solver}). " +
                "I'll send the charts when it's done. Send /cancel to stop it.",

            AlreadyActive active =>
                $"You already have job #{active.Existing.Id} in progress. " +
                "Wait for it, or send /cancel.",

            _ => "The queue is full. Please try again in a moment."
        };

        await replies.SendTextAsync(message, cancellationToken);
    }

    private async Task CancelAsync(
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var result = _queue.Cancel(chatId);

        var message = result switch
        {
            null => "You have no job in progress.",
            { WasRunning: true } => $"Cancelling job #{result.Job.Id}...",
            _ => $"Job #{result.Job.Id} cancelled."
        };

        await replies.SendTextAsync(message, cancellationToken);
    }
}
