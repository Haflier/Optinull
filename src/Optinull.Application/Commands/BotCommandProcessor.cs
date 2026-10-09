using Optinull.Application.Jobs;
using Optinull.Problems.JobShop.Benchmarks;

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

    public static string HelpText =>
        "I solve job shop scheduling problems and send back a Gantt chart " +
        "and a convergence chart.\n\n" +
        "/solve <benchmark> [sa|ga]\n" +
        $"Benchmarks: {string.Join(", ", JobShopBenchmarkCatalog.Names)}\n" +
        "sa = simulated annealing (default), ga = genetic algorithm\n\n" +
        "/cancel - stop your current job\n\n" +
        "Example: /solve ft10";

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
        if (!JobShopBenchmarkCatalog.TryGet(command.Benchmark, out _))
        {
            await replies.SendTextAsync(
                $"Unknown benchmark '{command.Benchmark}'. " +
                $"Available: {string.Join(", ", JobShopBenchmarkCatalog.Names)}.",
                cancellationToken);
            return;
        }

        var message = _queue.TryEnqueue(chatId, command, replies) switch
        {
            Enqueued enqueued =>
                $"Queued job #{enqueued.Job.Id} ({command.Benchmark}, {command.Solver}). " +
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
