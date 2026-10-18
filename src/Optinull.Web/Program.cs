using Optinull.Web.Contracts;

var builder = WebApplication.CreateBuilder(args);

// A problem is a few kilobytes of JSON; refuse anything much larger before it is read.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 256 * 1024);

var map = builder.Configuration.GetSection("Map").Get<MapViewOptions>() ?? new MapViewOptions();
var limits = GeoVrpLimits.Default;

builder.Services.AddSingleton(limits);
builder.Services.AddSingleton<GeoVrpValidator>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/config", () => new { map, limits });

app.MapPost(
    "/api/vrp/validate",
    (GeoVrpRequest request, GeoVrpValidator validator) => Results.Ok(validator.Validate(request)));

app.Run();

// Lets integration tests host the app.
public partial class Program { }
