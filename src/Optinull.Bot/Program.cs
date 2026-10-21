using Optinull.Infrastructure.Hosting;
using Optinull.Infrastructure.Routing;

var builder = Host.CreateApplicationBuilder(args);

var token = builder.Configuration["Telegram:BotToken"];

if (string.IsNullOrWhiteSpace(token))
{
    throw new InvalidOperationException(
        "Telegram:BotToken is not configured. Use 'dotnet user-secrets' locally " +
        "or the Telegram__BotToken environment variable.");
}

// Road routing powers /solve vrp map. The public OSRM demo server is for prototyping only;
// set Osrm:BaseUrl to your own instance for real use.
var osrm = builder.Configuration.GetSection("Osrm").Get<OsrmOptions>();

builder.Services.AddOptinullEngine();
builder.Services.AddOptinullRouting(osrm);
builder.Services.AddOptinullTelegram(token);

var host = builder.Build();
host.Run();
