using Optinull.Infrastructure.Hosting;

var builder = Host.CreateApplicationBuilder(args);

var token = builder.Configuration["Telegram:BotToken"];

if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException(
        "Telegram:BotToken is not configured. Use 'dotnet user-secrets' locally " +
        "or the Telegram__BotToken environment variable.");
}

// This host has no web app, so it has no map: /solve vrp map is only offered by Optinull.Web.
builder.Services.AddOptinullEngine();
builder.Services.AddOptinullTelegram(token);

var host = builder.Build();
host.Run();
