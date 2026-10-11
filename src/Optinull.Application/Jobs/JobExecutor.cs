using Optinull.Application.Commands;
using Optinull.Application.Comparison;
using Optinull.Application.Optimization;
using Optinull.Application.Problems;

namespace Optinull.Application.Jobs;

/// <summary>Runs one job: solve, reply with the charts. Honors cancel and a time limit.</summary>
public sealed class JobExecutor
{
    public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _timeLimit;

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
            await job.Replies.SendTextAsync(
                $"Solving job #{job.Id}: {job.Instance.Description} ({Describe(job)})...",
                token);

            var progress = new ReplyProgress(job.Replies, $"Job #{job.Id}: {Describe(job)}", token);

            var options = new SolveOptions(
                job.Command.Seed ?? SolveOptions.DefaultSeed,
                job.Command.Iterations,
                progress);

            var report = await Task.Run(
                () => Solve(job, options, token),
                token);

            // A solver that ignores the token still must not send a cancelled job's result.
            token.ThrowIfCancellationRequested();

            await progress.FinishAsync();

            foreach (var image in report.Images)
            {
                await job.Replies.SendPngAsync(
                    image.Png,
                    image.FileName,
                    image.Caption,
                    token);
            }

            if (report.Summary is not null)
                await job.Replies.SendTextAsync(report.Summary, token);

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

    private static string Describe(OptimizationJob job) =>
        job.Command.CompareAll ? "all solvers" : SolverNames.Display(job.Command.Solver);

    private static SolveReport Solve(OptimizationJob job, SolveOptions options, CancellationToken token) =>
        job.Command.CompareAll
            ? new SolverComparisonService().Compare(job.Instance, options, token)
            : job.Instance.Solve(job.Command.Solver, options, token);

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

    /// <summary>Forwards solver progress to the chat without ever failing the job.</summary>
    private sealed class ReplyProgress : IProgress<double>
    {
        private readonly IBotReplies _replies;
        private readonly string _label;
        private readonly CancellationToken _token;

        private int _reported;

        public ReplyProgress(IBotReplies replies, string label, CancellationToken token)
        {
            _replies = replies;
            _label = label;
            _token = token;
        }

        public void Report(double value)
        {
            Volatile.Write(ref _reported, 1);

            _ = SendAsync(value);
        }

        /// <summary>Shows 100%, but only if a bar was shown at all.</summary>
        public Task FinishAsync() =>
            Volatile.Read(ref _reported) == 1 ? SendAsync(1.0) : Task.CompletedTask;

        private async Task SendAsync(double value)
        {
            try
            {
                await _replies.ReportProgressAsync(value, _label, _token);
            }
            catch
            {
                // A progress bar is never worth failing a job.
            }
        }
    }
}
