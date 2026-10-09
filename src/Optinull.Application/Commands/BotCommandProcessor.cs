using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Application.Commands;

public sealed class BotCommandProcessor
{
    private const int MaxConcurrentSolves = 2;

    private readonly SemaphoreSlim _slots = new(MaxConcurrentSolves);
    private readonly JobShopSolveService _solver = new();
    private readonly JobShopReportService _reports = new();

    public static string HelpText =>
        "I solve job shop scheduling problems and send back a Gantt chart " +
        "and a convergence chart.\n\n" +
        "/solve <benchmark> [sa|ga]\n" +
        $"Benchmarks: {string.Join(", ", JobShopBenchmarkCatalog.Names)}\n" +
        "sa = simulated annealing (default), ga = genetic algorithm\n\n" +
        "Example: /solve ft10";

    public async Task ProcessAsync(
        string text,
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
                await SolveAsync(solve, replies, cancellationToken);
                break;
        }
    }

    private async Task SolveAsync(
        SolveCommand command,
        IBotReplies replies,
        CancellationToken cancellationToken)
    {
        if (!JobShopBenchmarkCatalog.TryGet(command.Benchmark, out var benchmark))
        {
            await replies.SendTextAsync(
                $"Unknown benchmark '{command.Benchmark}'. " +
                $"Available: {string.Join(", ", JobShopBenchmarkCatalog.Names)}.",
                cancellationToken);
            return;
        }

        if (!await _slots.WaitAsync(0, cancellationToken))
        {
            await replies.SendTextAsync(
                "I'm busy with other requests. Please try again in a moment.",
                cancellationToken);
            return;
        }

        try
        {
            await replies.SendTextAsync(
                $"Solving {benchmark.Name} ({command.Solver})...",
                cancellationToken);

            var (result, report) = await Task.Run(
                () =>
                {
                    var solved = _solver.Solve(
                        benchmark.Problem,
                        command.Solver,
                        cancellationToken: cancellationToken);

                    return (solved, _reports.Render(benchmark.Problem, solved));
                },
                cancellationToken);

            var gap = (result.Makespan - benchmark.KnownOptimum) / benchmark.KnownOptimum;

            var caption =
                $"{benchmark.Name} | {result.SolverName} | makespan {result.Makespan:0} " +
                $"(optimal {benchmark.KnownOptimum:0}, gap {gap:P1})";

            await replies.SendPngAsync(
                report.GanttPng, $"{benchmark.Name}-gantt.png", caption, cancellationToken);

            await replies.SendPngAsync(
                report.ConvergencePng, $"{benchmark.Name}-convergence.png", "Convergence", cancellationToken);
        }
        finally
        {
            _slots.Release();
        }
    }
}
