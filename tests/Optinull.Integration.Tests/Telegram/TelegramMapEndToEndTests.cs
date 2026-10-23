using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Optinull.Application.Routing;
using Optinull.Infrastructure.Hosting;
using Optinull.Infrastructure.Routing;
using Optinull.Infrastructure.Telegram;
using Optinull.Integration.Tests.Support;

namespace Optinull.Integration.Tests.Telegram;

public sealed class TelegramMapEndToEndTests
{
    /// <summary>
    /// A user's messages in, a map photo out: the poller, the processor, the real job worker, the
    /// OSRM and tile clients and the renderer, wired the way the bot host wires them. Only the
    /// network is fake.
    /// </summary>
    [Fact]
    public async Task LocationsInTheChat_ComeBackAsAMapPhoto()
    {
        var world = World.Create();
        var telegram = new FakeTelegram();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptinullEngine();
        services.AddSingleton<IRoutingClient>(new OsrmRoutingClient(
            new HttpClient(world),
            new OsrmOptions { BaseUrl = "https://osrm.test" }));
        services.AddSingleton<IMapTileClient>(new HttpMapTileClient(
            new HttpClient(world),
            new MapTileOptions { UrlTemplate = "https://tiles.test/{z}/{x}/{y}.png" }));
        services.AddSingleton(new TelegramBotOptions("123456:TEST-TOKEN", new HttpClient(telegram)));
        services.AddSingleton<TelegramPoller>();

        await using var provider = services.BuildServiceProvider();

        var worker = provider.GetServices<IHostedService>().OfType<JobWorker>().Single();

        using var stop = new CancellationTokenSource();

        await worker.StartAsync(stop.Token);

        var run = Task.Run(() => provider.GetRequiredService<TelegramPoller>().RunAsync(stop.Token));

        try
        {
            telegram.UserSendsText(7, "/solve vrp map greedy");
            telegram.UserSendsText(7, World.Places);
            telegram.UserPressesButton(7, 321, "m:solve");

            await Waiting.UntilAsync(() => telegram.Of("sendPhoto").Count >= 2, "the map and the chart to arrive");

            var photos = telegram.Of("sendPhoto");

            Assert.Contains(photos, photo => photo.Body.Contains("map |") && photo.Body.Length > 10_000);
            Assert.Contains(telegram.Texts, text => text.Contains("Fetching road distances"));
            Assert.Contains(telegram.Texts, text => text.StartsWith("Solving job #1"));

            Assert.Single(world.Tables);
            Assert.NotEmpty(world.Routes);
            Assert.NotEmpty(world.TileRequests);
        }
        finally
        {
            stop.Cancel();

            await run.WaitAsync(TimeSpan.FromSeconds(10));
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
