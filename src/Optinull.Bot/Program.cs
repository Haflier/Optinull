using Optinull.Application.Commands;
using Optinull.Application.Jobs;
using Optinull.Bot;
using Optinull.Infrastructure.Telegram;

var builder = Host.CreateApplicationBuilder(args);

var token = builder.Configuration["Telegram:BotToken"];

if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException(
        "Telegram:BotToken is not configured. Use 'dotnet user-secrets' locally " +
        "or the Telegram__BotToken environment variable.");
}

builder.Services.AddSingleton(new TelegramBotOptions(token));
builder.Services.AddSingleton(new JobQueue());
builder.Services.AddSingleton(new JobExecutor());
builder.Services.AddSingleton<BotCommandProcessor>();
builder.Services.AddSingleton<TelegramPoller>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<JobWorker>();

var host = builder.Build();
host.Run();
