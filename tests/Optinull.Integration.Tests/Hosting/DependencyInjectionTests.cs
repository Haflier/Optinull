using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Optinull.Application.Commands;
using Optinull.Application.Routing;
using Optinull.Infrastructure.Hosting;
using Optinull.Infrastructure.Routing;
using Optinull.Infrastructure.Telegram;
using Optinull.Integration.Tests.Support;

namespace Optinull.Integration.Tests.Hosting;

/// <summary>The wiring the bot host relies on: registrations and how settings turn into options.</summary>
public sealed class DependencyInjectionTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider Build(IConfiguration? config, bool routing = true)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptinullEngine();

        if (routing)
        {
            services.AddOptinullRouting(
                config?.GetSection("Osrm").Get<OsrmOptions>(),
                config?.GetSection("MapTiles").Get<MapTileOptions>());
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task WithoutSettings_TheDefaultsApply()
    {
        await using var provider = Build(null);

        var routing = provider.GetRequiredService<IRoutingClient>();
        var tiles = provider.GetRequiredService<IMapTileClient>();

        Assert.Equal(100, routing.MaxLocations);
        Assert.Contains("OpenStreetMap", tiles.Attribution);
    }

    [Fact]
    public async Task TheClients_AreSharedByEveryChat()
    {
        await using var provider = Build(null);

        Assert.Same(provider.GetRequiredService<IRoutingClient>(), provider.GetRequiredService<IRoutingClient>());
        Assert.Same(provider.GetRequiredService<IMapTileClient>(), provider.GetRequiredService<IMapTileClient>());
    }

    [Fact]
    public async Task SettingsFromConfiguration_ReachTheClients()
    {
        var config = Config(new()
        {
            ["Osrm:MaxLocations"] = "42",
            ["MapTiles:Attribution"] = "\u00a9 Test provider"
        });

        await using var provider = Build(config);

        Assert.Equal(42, provider.GetRequiredService<IRoutingClient>().MaxLocations);
        Assert.Equal("\u00a9 Test provider", provider.GetRequiredService<IMapTileClient>().Attribution);
    }

    [Fact]
    public void TheOptions_BindFromConfiguration_IncludingTheInitOnlyProperties()
    {
        var config = Config(new()
        {
            ["Osrm:BaseUrl"] = "https://osrm.example",
            ["Osrm:Profile"] = "car",
            ["Osrm:MaxLocations"] = "50",
            ["Osrm:Timeout"] = "00:00:05",
            ["MapTiles:UrlTemplate"] = "https://tiles.example/{z}/{x}/{y}.png?key=abc",
            ["MapTiles:Attribution"] = "\u00a9 Example",
            ["MapTiles:UserAgent"] = "Example/2.0",
            ["MapTiles:CacheEntries"] = "7",
            ["MapTiles:Timeout"] = "00:00:03"
        });

        var osrm = config.GetSection("Osrm").Get<OsrmOptions>()!;
        var tiles = config.GetSection("MapTiles").Get<MapTileOptions>()!;

        Assert.Equal("https://osrm.example", osrm.BaseUrl);
        Assert.Equal("car", osrm.Profile);
        Assert.Equal(50, osrm.MaxLocations);
        Assert.Equal(TimeSpan.FromSeconds(5), osrm.Timeout);

        Assert.Equal("https://tiles.example/{z}/{x}/{y}.png?key=abc", tiles.UrlTemplate);
        Assert.Equal("\u00a9 Example", tiles.Attribution);
        Assert.Equal("Example/2.0", tiles.UserAgent);
        Assert.Equal(7, tiles.CacheEntries);
        Assert.Equal(TimeSpan.FromSeconds(3), tiles.Timeout);
    }

    [Fact]
    public void EnvironmentVariables_WithDoubleUnderscores_BindToTheSameSettings()
    {
        // The bot is configured like this in production: MapTiles__UrlTemplate and so on.
        const string prefix = "OPTINULL_ITEST_";

        Environment.SetEnvironmentVariable(prefix + "MapTiles__UrlTemplate", "https://tiles.example/{z}/{x}/{y}.png");
        Environment.SetEnvironmentVariable(prefix + "MapTiles__Attribution", "\u00a9 Env provider");
        Environment.SetEnvironmentVariable(prefix + "Osrm__BaseUrl", "https://osrm.env.example");

        try
        {
            var config = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();

            var tiles = config.GetSection("MapTiles").Get<MapTileOptions>()!;
            var osrm = config.GetSection("Osrm").Get<OsrmOptions>()!;

            Assert.Equal("https://tiles.example/{z}/{x}/{y}.png", tiles.UrlTemplate);
            Assert.Equal("\u00a9 Env provider", tiles.Attribution);
            Assert.Equal("https://osrm.env.example", osrm.BaseUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "MapTiles__UrlTemplate", null);
            Environment.SetEnvironmentVariable(prefix + "MapTiles__Attribution", null);
            Environment.SetEnvironmentVariable(prefix + "Osrm__BaseUrl", null);
        }
    }

    [Fact]
    public async Task AnEmptyTemplateFromConfiguration_TurnsTheBackgroundOff()
    {
        var config = Config(new() { ["MapTiles:UrlTemplate"] = "" });
        var options = config.GetSection("MapTiles").Get<MapTileOptions>()!;

        var requests = 0;
        var handler = new CountingHandler(() => requests++);

        var client = new HttpMapTileClient(new HttpClient(handler), options);

        Assert.Null(await client.GetTileAsync(10, 1, 1));
        Assert.Equal(0, requests);
    }

    [Fact]
    public async Task TheProcessor_OffersTheMap_OnlyWhenRoutingIsRegistered()
    {
        await using var with = Build(null);
        await using var without = Build(null, routing: false);

        var withReplies = new CapturingReplies();
        var withoutReplies = new CapturingReplies();

        await with.GetRequiredService<BotCommandProcessor>().ProcessAsync("/solve vrp map greedy", 1, withReplies);
        await without.GetRequiredService<BotCommandProcessor>().ProcessAsync("/solve vrp map greedy", 1, withoutReplies);

        Assert.Contains("depot", withReplies.Texts[^1]);
        Assert.Contains("not available", withoutReplies.Texts[^1]);
    }

    [Fact]
    public async Task TheEngine_StartsTheJobWorker()
    {
        await using var provider = Build(null);

        Assert.Single(provider.GetServices<IHostedService>().OfType<JobWorker>());
    }

    [Fact]
    public async Task Telegram_NeedsATokenAndWiresThePoller()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptinullEngine();
        services.AddOptinullTelegram("123456:TEST-TOKEN");

        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<TelegramPoller>());
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddOptinullTelegram(" "));
    }

    private sealed class CountingHandler(Action onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
