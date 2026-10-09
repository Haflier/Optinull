using Optinull.Application.Commands;
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
builder.Services.AddSingleton<BotCommandProcessor>();
builder.Services.AddSingleton<TelegramPoller>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
