using Microsoft.Extensions.Logging.Abstractions;
using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Infrastructure.Routing;
using Optinull.Infrastructure.Telegram;

namespace Optinull.Integration.Tests.Support;

/// <summary>A real poller and processor talking to a fake Telegram, until it is disposed.</summary>
internal sealed class PollerHarness : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _run;

    private PollerHarness(FakeTelegram telegram, World world, BotCommandProcessor processor)
    {
        Telegram = telegram;
        World = world;

        var poller = new TelegramPoller(
            new TelegramBotOptions("123456:TEST-TOKEN", new HttpClient(telegram)),
            processor,
            NullLogger<TelegramPoller>.Instance);

        _run = Task.Run(() => poller.RunAsync(_stop.Token));
    }

    public FakeTelegram Telegram { get; }

    public World World { get; }

    public static PollerHarness Start()
    {
        var world = World.Create();

        var routing = new OsrmRoutingClient(
            new HttpClient(world),
            new OsrmOptions { BaseUrl = "https://osrm.test" });

        var tiles = new HttpMapTileClient(
            new HttpClient(world),
            new MapTileOptions { UrlTemplate = "https://tiles.test/{z}/{x}/{y}.png" });

        var processor = new BotCommandProcessor(new JobQueue(), null, null, routing, tiles);

        return new PollerHarness(new FakeTelegram(), world, processor);
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();

        try
        {
            await _run.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (OperationCanceledException)
        {
        }

        _stop.Dispose();
    }
}
