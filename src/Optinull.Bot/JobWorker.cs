using Optinull.Application.Jobs;

namespace Optinull.Bot;

/// <summary>Takes queued jobs and runs them, a few at a time.</summary>
public sealed class JobWorker : BackgroundService
{
    private const int Parallelism = 2;

    private readonly JobQueue _queue;
    private readonly JobExecutor _executor;
    private readonly ILogger<JobWorker> _logger;

    public JobWorker(
        JobQueue queue,
        JobExecutor executor,
        ILogger<JobWorker> logger)
    {
        _queue = queue;
        _executor = executor;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            Enumerable
                .Range(0, Parallelism)
                .Select(_ => RunLoopAsync(stoppingToken)));

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await _executor.ExecuteAsync(job, stoppingToken);

                    _logger.LogInformation(
                        "Job {JobId} ({Target}, {Solver}) {Status} {Error}",
                        job.Id,
                        job.Command.Target,
                        job.Command.Solver,
                        job.Status,
                        job.Error);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Job {JobId} crashed the worker loop.", job.Id);
                }
                finally
                {
                    _queue.Release(job);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
