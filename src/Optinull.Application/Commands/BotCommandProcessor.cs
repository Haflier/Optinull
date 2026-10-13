using System.Text;
using Optinull.Application.Jobs;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;

namespace Optinull.Application.Commands;

/// <summary>Understands chat commands; solving itself happens on the job queue.</summary>
public sealed class BotCommandProcessor
{
    private readonly JobQueue _queue;
    private readonly ProblemRegistry _registry;
    private readonly ConversationStore _conversations;

    public BotCommandProcessor(
        JobQueue queue,
        ProblemRegistry? registry = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(queue);

        _queue = queue;
        _registry = registry ?? ProblemRegistry.CreateDefault();
        _conversations = new ConversationStore(timeProvider ?? TimeProvider.System);
    }

    public string StartText
    {
        get
        {
            var text = new StringBuilder();

            text.AppendLine("Welcome to Optinull.");
            text.AppendLine(
                "Optimization engine for solving and comparing optimization problems.");
            text.AppendLine();
            text.AppendLine("What would you like to do?");
            text.AppendLine("/optimize - build a problem step by step using buttons");
            text.AppendLine("/solve - solve a problem directly with a command");
            text.AppendLine("/compare - compare optimization algorithms");
            text.AppendLine();
            text.Append("Use /help to see all commands and input formats.");

            return text.ToString();
        }
    }

    public string HelpText
    {
        get
        {
            var text = new StringBuilder();

            text.AppendLine("Optinull Help");
            text.AppendLine("Solve and compare optimization problems.");
            text.AppendLine();
            text.AppendLine("Commands");
            text.AppendLine("/optimize - choose a problem, dataset and algorithm using buttons");
            text.AppendLine("  Choose Custom to enter your own problem step by step.");
            text.AppendLine("/compare <problem> - run every algorithm on the same problem");
            text.AppendLine(
                "/solve <problem> [greedy|hc|ils|sa|ga|all] [iterations=N] [seed=N] [key=value ...]");
            text.AppendLine("/cancel - stop your current job");
            text.AppendLine();
            text.AppendLine(
                $"Built-in instances: {string.Join(", ", _registry.BuiltInNames)}");
            text.AppendLine();
            text.AppendLine("Algorithms");
            text.AppendLine("greedy - fast heuristic");
            text.AppendLine("hc - hill climbing");
            text.AppendLine("ils - iterated local search");
            text.AppendLine("sa - simulated annealing (default)");
            text.AppendLine("ga - genetic algorithm");
            text.AppendLine("all - compare all algorithms");
            text.AppendLine();
            text.AppendLine(
                $"iterations - candidate evaluations for ILS, SA and GA, " +
                $"{SolveLimits.MinIterations}-{SolveLimits.MaxIterations} " +
                "(default: solver's own setting)");
            text.AppendLine("seed - use the same seed to reproduce a result");
            text.AppendLine();
            text.AppendLine("Custom input");
            text.AppendLine(
                "Provide the instance on the lines after /solve <problem> [solver],");
            text.AppendLine("or attach a .txt file with the command as its caption.");
            text.AppendLine("Alternatively, provide problem settings to generate a random instance.");

            foreach (var module in _registry.Modules)
            {
                text.AppendLine();
                text.AppendLine($"{module.Id} ({module.Title}): {module.CustomInputHelp}");
                text.AppendLine(
                    $"Random instance: /solve {module.Id} " +
                    $"{ProblemParameterReader.Describe(module.Parameters)}");
            }

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

        var command = BotCommandParser.Parse(text);

        // Plain text while the user is entering their own data is an answer.
        if (!text.TrimStart().StartsWith('/') &&
            _conversations.Get(chatId) is Collecting collecting)
        {
            await ContinueDialogAsync(collecting, chatId, text, replies, cancellationToken);
            return;
        }

        // Starting something new abandons a half-finished dialog.
        if (command is OptimizeCommand or SolveCommand)
            _conversations.Clear(chatId);

        switch (command)
        {
            case StartCommand:
                await replies.SendTextAsync(StartText, cancellationToken);
                break;

            case HelpCommand:
                await replies.SendTextAsync(HelpText, cancellationToken);
                break;

            case InvalidCommand invalid:
                await replies.SendTextAsync(invalid.Message, cancellationToken);
                break;

            case OptimizeCommand:
                await ShowProblemsAsync(replies, cancellationToken);
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
        var settings = command.ParameterValues;

        if (module is not null)
        {
            if (command.InstanceText is not null)
            {
                // "/solve tsp" with lines: the user's own instance.
                if (settings.Count > 0)
                {
                    await replies.SendTextAsync(
                        $"Send either instance lines or settings ({string.Join(", ", settings.Keys)}), not both.",
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
            else if (settings.Count > 0)
            {
                // "/solve tsp cities=30": a random instance.
                var seed = command.Seed ?? SolveOptions.DefaultSeed;

                if (!module.TryGenerate(settings, seed, out instance, out var error))
                {
                    await replies.SendTextAsync(
                        $"Invalid settings: {error}\n\n" +
                        $"{module.Id}: {ProblemParameterReader.Describe(module.Parameters)}",
                        cancellationToken);
                    return;
                }
            }
            else
            {
                await replies.SendTextAsync(
                    "Send the instance on the lines after the command, or attach it as " +
                    $"a .txt file, or ask for a random one: " +
                    $"/solve {module.Id} {ProblemParameterReader.Describe(module.Parameters)}" +
                    $"\n\n{module.Id} ({module.Title}): {module.CustomInputHelp}",
                    cancellationToken);
                return;
            }
        }
        else if (_registry.TryGetBuiltIn(command.Target, out instance))
        {
            if (settings.Count > 0)
            {
                await replies.SendTextAsync(
                    $"{command.Target} is a fixed instance, so it takes no settings " +
                    $"({string.Join(", ", settings.Keys)}). Only the solver, iterations= and seed= apply.",
                    cancellationToken);
                return;
            }
        }
        else
        {
            await replies.SendTextAsync(
                $"Unknown instance '{command.Target}'. " +
                $"Built-in: {string.Join(", ", _registry.BuiltInNames)}. " +
                $"For your own data use: {string.Join(", ", _registry.Modules.Select(m => m.Id))}.",
                cancellationToken);
            return;
        }

        await QueueAsync(command, instance!, chatId, replies, cancellationToken);
    }

    private async Task QueueAsync(
        SolveCommand command,
        IProblemInstance instance,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        // The parsed instance travels with the job; drop the raw text.
        var queued = command with { InstanceText = null };

        var message = _queue.TryEnqueue(chatId, queued, replies, instance) switch
        {
            Enqueued enqueued =>
                $"Queued job #{enqueued.Job.Id} ({instance.Description}, " +
                $"{(command.CompareAll ? "all solvers" : SolverNames.Display(command.Solver))}" +
                $"{(command.Iterations is { } n ? $", {n} iterations" : string.Empty)}). " +
                "I'll send the charts when it's done. Send /cancel to stop it.",

            AlreadyActive active =>
                $"You already have job #{active.Existing.Id} in progress. " +
                "Wait for it, or send /cancel.",

            _ => "The queue is full. Please try again in a moment."
        };

        await replies.SendTextAsync(message, cancellationToken);
    }

    // ---- The /optimize wizard ------------------------------------------------
    // Every button carries the whole choice so far ("o:tsp:small:ga"), so the
    // wizard needs no state on the server.

    private static readonly (string Key, string Label)[] Algorithms =
    [
        ("greedy", "Greedy"),
        ("hc", "Hill Climbing"),
        ("ils", "Iterated Local Search"),
        ("sa", "Simulated Annealing"),
        ("ga", "Genetic Algorithm"),
        ("all", "Compare All")
    ];

    private static IReadOnlyList<Choice> Button(string label, string data) =>
        [new Choice(label, data)];

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private Task ShowProblemsAsync(IBotReplies replies, CancellationToken cancellationToken) =>
        replies.ShowChoicesAsync(
            "Choose a problem:",
            _registry.Modules
                .Select(module => Button(Capitalize(module.Title), $"o:{module.Id}"))
                .ToList(),
            cancellationToken);

    /// <summary>Handles a button press from the /optimize wizard.</summary>
    public async Task ProcessCallbackAsync(
        string? data,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replies);

        var parts = (data ?? string.Empty).Split(':');

        if (parts.Length < 2 || parts.Length > 4 || parts[0] != "o")
            return;

        var module = _registry.FindModule(parts[1]);

        if (module is null)
        {
            await SendStaleAsync(replies, cancellationToken);
            return;
        }

        switch (parts.Length)
        {
            case 2:
                await ShowDatasetsAsync(module, replies, cancellationToken);
                break;

            case 3 when parts[2] == "custom":
                await StartCustomDialogAsync(module, chatId, replies, cancellationToken);
                break;

            case 3 when module.Presets.Any(preset => preset.Key == parts[2]):
                await ShowAlgorithmsAsync(module, parts[2], replies, cancellationToken);
                break;

            case 4 when parts[2] == "custom":
                await StartCustomJobAsync(module, parts[3], chatId, replies, cancellationToken);
                break;

            case 4:
                await StartFromWizardAsync(module, parts[2], parts[3], chatId, replies, cancellationToken);
                break;

            default:
                await SendStaleAsync(replies, cancellationToken);
                break;
        }
    }

    private static Task SendStaleAsync(IBotReplies replies, CancellationToken cancellationToken) =>
        replies.SendTextAsync(
            "That menu is out of date. Send /optimize to start again.",
            cancellationToken);

    private Task ShowDatasetsAsync(
        IProblemModule module,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var rows = module.Presets
            .Select(preset => Button(preset.Label, $"o:{module.Id}:{preset.Key}"))
            .Append(Button("Custom", $"o:{module.Id}:custom"))
            .ToList();

        return replies.ShowChoicesAsync(
            $"{Capitalize(module.Title)}\nChoose dataset:",
            rows,
            cancellationToken);
    }

    private static List<IReadOnlyList<Choice>> AlgorithmRows(string moduleId, string datasetKey) =>
        Algorithms
            .Select(algorithm => Button(algorithm.Label, $"o:{moduleId}:{datasetKey}:{algorithm.Key}"))
            .ToList();

    private static (SolverKind Solver, bool Compare) ParseAlgorithm(string key) =>
        key switch
        {
            "greedy" => (SolverKind.Greedy, false),
            "hc" => (SolverKind.HillClimbing, false),
            "ils" => (SolverKind.IteratedLocalSearch, false),
            "ga" => (SolverKind.GeneticAlgorithm, false),
            "all" => (SolverKind.SimulatedAnnealing, true),
            _ => (SolverKind.SimulatedAnnealing, false)
        };

    private Task ShowAlgorithmsAsync(
        IProblemModule module,
        string presetKey,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var preset = module.Presets.First(candidate => candidate.Key == presetKey);

        return replies.ShowChoicesAsync(
            $"{Capitalize(module.Title)}: {preset.Label}\nChoose algorithm:",
            AlgorithmRows(module.Id, presetKey),
            cancellationToken);
    }

    private async Task StartFromWizardAsync(
        IProblemModule module,
        string presetKey,
        string algorithmKey,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var preset = module.Presets.FirstOrDefault(candidate => candidate.Key == presetKey);
        var algorithm = Algorithms.FirstOrDefault(candidate => candidate.Key == algorithmKey);

        if (preset is null || algorithm.Key is null)
        {
            await SendStaleAsync(replies, cancellationToken);
            return;
        }

        var (solver, compare) = ParseAlgorithm(algorithm.Key);

        // Replace the buttons with what was chosen, then start like /solve does.
        await replies.ShowChoicesAsync(
            $"Problem: {Capitalize(module.Title)}\nDataset: {preset.Label}\nAlgorithm: {algorithm.Label}",
            [],
            cancellationToken);

        await EnqueueAsync(
            new SolveCommand(preset.Target, solver, null, null, null, preset.Settings, compare),
            chatId,
            replies,
            cancellationToken);
    }

    // ---- Entering your own data, one question at a time -----------------------

    private async Task StartCustomDialogAsync(
        IProblemModule module,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var dialog = module.CreateDialog();

        _conversations.Set(chatId, new Collecting(module, dialog));

        await replies.ShowChoicesAsync(
            $"Problem: {Capitalize(module.Title)}\nDataset: Custom",
            [],
            cancellationToken);

        await replies.SendTextAsync(
            $"{dialog.Start()}\n\n(Send /cancel to stop.)",
            cancellationToken);
    }

    private async Task ContinueDialogAsync(
        Collecting state,
        long chatId,
        string text,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        switch (state.Dialog.Answer(text))
        {
            case DialogAsk ask:
                await replies.SendTextAsync(ask.Text, cancellationToken);
                break;

            case DialogFailed failed:
                _conversations.Clear(chatId);
                await replies.SendTextAsync(
                    $"{failed.Message}\n\nSend /optimize to start again.",
                    cancellationToken);
                break;

            case DialogDone done:
                _conversations.Set(chatId, new Ready(state.Module, done.Instance));

                var recap = done.Notes is null ? string.Empty : done.Notes + "\n\n";

                await replies.ShowChoicesAsync(
                    $"{recap}Got it: {done.Instance.Description}.\nChoose algorithm:",
                    AlgorithmRows(state.Module.Id, "custom"),
                    cancellationToken);
                break;
        }
    }

    private async Task StartCustomJobAsync(
        IProblemModule module,
        string algorithmKey,
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        var algorithm = Algorithms.FirstOrDefault(candidate => candidate.Key == algorithmKey);

        if (algorithm.Key is null ||
            _conversations.Get(chatId) is not Ready ready ||
            ready.Module.Id != module.Id)
        {
            await SendStaleAsync(replies, cancellationToken);
            return;
        }

        _conversations.Clear(chatId);

        var (solver, compare) = ParseAlgorithm(algorithm.Key);

        await replies.ShowChoicesAsync(
            $"Problem: {Capitalize(module.Title)}\nDataset: Custom ({ready.Instance.Description})\n" +
            $"Algorithm: {algorithm.Label}",
            [],
            cancellationToken);

        await QueueAsync(
            new SolveCommand("custom", solver, CompareAll: compare),
            ready.Instance,
            chatId,
            replies,
            cancellationToken);
    }

    private async Task CancelAsync(
        long chatId,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        // Cancelling in the middle of entering data just drops the data.
        if (_conversations.Clear(chatId))
        {
            await replies.SendTextAsync(
                "Cancelled. Send /optimize to start again.",
                cancellationToken);
            return;
        }

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
