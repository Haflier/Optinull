using System.Text;
using Optinull.Application.Jobs;
using Optinull.Application.Problems;

namespace Optinull.Application.Commands;

/// <summary>Understands chat commands; solving itself happens on the job queue.</summary>
public sealed class BotCommandProcessor
{
    private readonly JobQueue _queue;
    private readonly ProblemRegistry _registry;

    public BotCommandProcessor(JobQueue queue, ProblemRegistry? registry = null)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
        _registry = registry ?? ProblemRegistry.CreateDefault();
    }

    public string HelpText
    {
        get
        {
            var text = new StringBuilder();

            text.AppendLine("I solve optimization problems and send back charts.");
            text.AppendLine();
            text.AppendLine("/solve <instance> [sa|ga]");
            text.AppendLine($"Built-in instances: {string.Join(", ", _registry.BuiltInNames)}");
            text.AppendLine("sa = simulated annealing (default), ga = genetic algorithm");
            text.AppendLine();
            text.AppendLine(
                "Your own problem: /solve <problem> [sa|ga] with the instance on the " +
                "next lines, or attach a .txt file with that as the caption.");

            foreach (var module in _registry.Modules)
            {
                text.AppendLine();
                text.AppendLine($"{module.Id} ({module.Title}): {module.CustomInputHelp}");
            }

            text.AppendLine();
            text.Append("/cancel - stop your current job");

            return text.ToString();
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
        IProblemInstance? instance;

        var module = _registry.FindModule(command.Target);

        if (module is not null)
        {
            // "/solve tsp": the instance follows the command.
            if (command.InstanceText is null)
            {
                await replies.SendTextAsync(
                    "Send the instance on the lines after the command, or attach it as " +
                    $"a .txt file.\n\n{module.Id} ({module.Title}): {module.CustomInputHelp}",
                    cancellationToken);
                return;
            }

            if (!module.TryParseCustom(command.InstanceText, out instance, out var error))
            {
                await replies.SendTextAsync(
                    $"Invalid instance: {error}\n\nSend /help to see the format.",
                    cancellationToken);
                return;
            }
        }
        else if (!_registry.TryGetBuiltIn(command.Target, out instance))
        {
            await replies.SendTextAsync(
                $"Unknown instance '{command.Target}'. " +
                $"Built-in: {string.Join(", ", _registry.BuiltInNames)}. " +
                $"For your own data use: {string.Join(", ", _registry.Modules.Select(m => m.Id))}.",
                cancellationToken);
            return;
        }

        // The parsed instance travels with the job; drop the raw text.
        var queued = command with { InstanceText = null };

        var message = _queue.TryEnqueue(chatId, queued, replies, instance!) switch
        {
            Enqueued enqueued =>
                $"Queued job #{enqueued.Job.Id} ({instance!.Description}, {command.Solver}). " +
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
