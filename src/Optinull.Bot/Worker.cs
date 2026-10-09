using Optinull.Infrastructure.Telegram;

namespace Optinull.Bot;

public sealed class Worker : BackgroundService
{
    private readonly TelegramPoller _poller;

    public Worker(TelegramPoller poller)
    {
        _poller = poller;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        _poller.RunAsync(stoppingToken);
}
