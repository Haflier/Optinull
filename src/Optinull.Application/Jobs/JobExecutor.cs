using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Application.Jobs;

/// <summary>Runs one job: solve, render, reply. Honors cancel and a time limit.</summary>
public sealed class JobExecutor
{
    public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _timeLimit;
    private readonly JobShopSolveService _solver = new();
    private readonly JobShopReportService _reports = new();

    public JobExecutor(TimeSpan? timeLimit = null)
    {
        if (timeLimit is { } limit && limit <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(
                nameof(timeLimit),
                "Time limit must be greater than zero.");

        _timeLimit = timeLimit ?? DefaultTimeLimit;
    }

    public async Task ExecuteAsync(
        OptimizationJob job,
        CancellationToken stoppingToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        // Cancelled while it was still waiting in the queue.
        if (!job.TryStart())
            return;

        using var timeout = new CancellationTokenSource(_timeLimit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            job.Token,
            timeout.Token,
            stoppingToken);

        var token = linked.Token;

        try
        {
            if (Resolve(job) is not { } target)
            {
                job.Fail($"Unknown benchmark '{job.Command.Benchmark}'.");
                await TrySendAsync(job, $"Job #{job.Id} failed: unknown benchmark.");
                return;
            }

            await job.Replies.SendTextAsync(
                $"Solving job #{job.Id}: {target.Name} ({job.Command.Solver})...",
                token);

            var (result, report) = await Task.Run(
                () =>
                {
                    var solved = _solver.Solve(
                        target.Problem,
                        job.Command.Solver,
                        cancellationToken: token);

                    return (solved, _reports.Render(target.Problem, solved));
                },
                token);

            // A solver that ignores the token still must not send a cancelled job's result.
            token.ThrowIfCancellationRequested();

            await job.Replies.SendPngAsync(
                report.GanttPng,
                $"{target.Name}-gantt.png",
                Caption(target, result),
                token);

            await job.Replies.SendPngAsync(
                report.ConvergencePng,
                $"{target.Name}-convergence.png",
                "Convergence",
                token);

            job.Complete();
        }
        catch (OperationCanceledException)
        {
            if (job.CancelRequested)
            {
                job.MarkCancelled();
                await TrySendAsync(job, $"Job #{job.Id} cancelled.");
            }
            else if (timeout.IsCancellationRequested)
            {
                job.Fail($"Timed out after {_timeLimit.TotalSeconds:0} seconds.");
                await TrySendAsync(
                    job,
                    $"Job #{job.Id} stopped: it took longer than {_timeLimit.TotalSeconds:0} seconds.");
            }
            else
            {
                // The host is shutting down.
                job.MarkCancelled();
            }
        }
        catch (Exception exception)
        {
            job.Fail(exception.Message);
            await TrySendAsync(job, $"Job #{job.Id} failed. Please try again.");
        }
    }

    private sealed record Target(string Name, JobShopProblem Problem, double? KnownOptimum);

    private static Target? Resolve(OptimizationJob job)
    {
        if (job.CustomProblem is { } custom)
            return new Target("custom", custom, null);

        return JobShopBenchmarkCatalog.TryGet(job.Command.Benchmark, out var benchmark)
            ? new Target(benchmark.Name, benchmark.Problem, benchmark.KnownOptimum)
            : null;
    }

    private static string Caption(Target target, JobShopSolveResult result)
    {
        var prefix = $"{target.Name} | {result.SolverName} | makespan {result.Makespan:0}";

        if (target.KnownOptimum is { } optimum)
        {
            var gap = (result.Makespan - optimum) / optimum;

            return $"{prefix} (optimal {optimum:0}, gap {gap:P1})";
        }

        // The optimum of a custom problem is unknown, so compare with a lower bound.
        var lowerBound = JobShopLowerBound.Compute(target.Problem);
        var above = (result.Makespan - lowerBound) / lowerBound;

        return $"{prefix} (lower bound {lowerBound:0}, at most {above:P1} above optimal)";
    }

    private static async Task TrySendAsync(OptimizationJob job, string text)
    {
        try
        {
            await job.Replies.SendTextAsync(text, CancellationToken.None);
        }
        catch
        {
            // The chat may be unreachable; the job outcome is already recorded.
        }
    }
}
