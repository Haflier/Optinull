using Microsoft.Extensions.Hosting;
using Optinull.Infrastructure.Telegram;

namespace Optinull.Infrastructure.Hosting;

/// <summary>Runs the Telegram long-polling loop for as long as the host runs.</summary>
public sealed class TelegramWorker : BackgroundService
{
    private readonly TelegramPoller _poller;

    public TelegramWorker(TelegramPoller poller)
    {
        _poller = poller;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _poller.RunAsync(stoppingToken);
}
