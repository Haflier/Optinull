namespace Optinull.Application.Jobs;

/// <summary>Runs one job: solve, reply with the charts. Honors cancel and a time limit.</summary>
public sealed class JobExecutor
{
    public static readonly TimeSpan DefaultTimeLimit = TimeSpan.FromSeconds(60);

    private const int Seed = 42;

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
                $"Solving job #{job.Id}: {job.Instance.Description} ({job.Command.Solver})...",
                token);

            var report = await Task.Run(
                () => job.Instance.Solve(job.Command.Solver, Seed, token),
                token);

            // A solver that ignores the token still must not send a cancelled job's result.
            token.ThrowIfCancellationRequested();

            foreach (var image in report.Images)
            {
                await job.Replies.SendPngAsync(
                    image.Png,
                    image.FileName,
                    image.Caption,
                    token);
            }

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
