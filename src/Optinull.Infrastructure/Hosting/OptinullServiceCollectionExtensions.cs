using Microsoft.Extensions.DependencyInjection;
using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Application.Problems;
using Optinull.Infrastructure.Telegram;

namespace Optinull.Infrastructure.Hosting;

/// <summary>The wiring the bot host and the web host share, so neither repeats it.</summary>
public static class OptinullServiceCollectionExtensions
{
    /// <summary>
    /// The problem registry, the job queue and its workers, and the command processor. A host
    /// that wants map sessions registers an <c>IMapSessionLauncher</c> as well; the processor
    /// picks it up when it is there.
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
