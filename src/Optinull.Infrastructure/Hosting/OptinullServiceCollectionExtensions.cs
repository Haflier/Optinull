using Microsoft.Extensions.DependencyInjection;
using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Problems;
using Optinull.Application.Routing;
using Optinull.Infrastructure.Routing;
using Optinull.Infrastructure.Telegram;

namespace Optinull.Infrastructure.Hosting;

/// <summary>The wiring of the bot host.</summary>
public static class OptinullServiceCollectionExtensions
{
    /// <summary>
    /// The problem registry, the job queue and its workers, and the command processor. A host
    /// that wants map routing registers an <c>IRoutingClient</c> as well (see
    /// <see cref="AddOptinullRouting"/>); the processor picks it up when it is there.
    /// </summary>
    public static IServiceCollection AddOptinullEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(ProblemRegistry.CreateDefault());
        services.AddSingleton(new JobQueue());
        services.AddSingleton(new JobExecutor());
        services.AddSingleton<BotCommandProcessor>();
        services.AddHostedService<JobWorker>();

        return services;
    }

    /// <summary>Registers the OSRM road routing and the map tiles used by /solve vrp map.</summary>
    public static IServiceCollection AddOptinullRouting(
        this IServiceCollection services,
        OsrmOptions? options = null,
        MapTileOptions? tileOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // One client for the whole app: it remembers the distance matrices it has already fetched.
        services.AddSingleton<IRoutingClient>(_ =>
        {
            var http = new HttpClient();

            http.DefaultRequestHeaders.UserAgent.ParseAdd("Optinull/1.0");

            return new OsrmRoutingClient(http, options);
        });

        // One client for the whole app, so the tile cache is shared by every chat.
        services.AddSingleton<IMapTileClient>(_ =>
        {
            var http = new HttpClient();

            http.DefaultRequestHeaders.UserAgent.ParseAdd((tileOptions ?? new MapTileOptions()).UserAgent);

            return new HttpMapTileClient(http, tileOptions);
        });

        return services;
    }

    /// <summary>Starts the Telegram bot. Needs <see cref="AddOptinullEngine"/>.</summary>
    public static IServiceCollection AddOptinullTelegram(this IServiceCollection services, string botToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(botToken);

        services.AddSingleton(new TelegramBotOptions(botToken));
        services.AddSingleton<TelegramPoller>();
        services.AddHostedService<TelegramWorker>();

        return services;
    }
}
