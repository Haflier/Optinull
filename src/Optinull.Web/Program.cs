using System.Text.Json.Serialization;
using Optinull.Application.Jobs;
using Optinull.Application.MapSessions;
using Optinull.Infrastructure.Hosting;
using Optinull.Infrastructure.Routing;
using Optinull.Problems.Vrp;
using Optinull.Web.Contracts;
using Optinull.Web.Sessions;

var builder = WebApplication.CreateBuilder(args);

// A problem is a few kilobytes of JSON; refuse anything much larger before it is read.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 256 * 1024);

var map = builder.Configuration.GetSection("Map").Get<MapViewOptions>() ?? new MapViewOptions();
var osrm = builder.Configuration.GetSection("Osrm").Get<OsrmOptions>() ?? new OsrmOptions();
var sessionOptions = builder.Configuration.GetSection("MapSessions").Get<MapSessionOptions>() ?? new MapSessionOptions();
var limits = GeoVrpLimits.From(VrpInputLimits.Default, osrm);

const string busyMessage =
    "You already have an optimization in progress. Wait for it to finish, or send /cancel to the bot.";

// Phases such as "Running" go to the page as words, not numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(limits);
builder.Services.AddSingleton<GeoVrpValidator>();

// One client for the whole app: it remembers the distance matrices it has already fetched.
builder.Services.AddSingleton<IRoutingClient>(_ =>
{
    var http = new HttpClient();

    http.DefaultRequestHeaders.UserAgent.ParseAdd("Optinull/1.0");

    return new OsrmRoutingClient(http, osrm);
});

// The Telegram bot runs inside this app, because map sessions live in this app's memory: the
// bot creates them, the page uses them, and both have to see the same ones.
builder.Services.AddOptinullEngine();
builder.Services.AddSingleton(sp => new MapSessionService(sp.GetRequiredService<JobQueue>(), sessionOptions));
builder.Services.AddSingleton<IMapSessionLauncher>(sp => sp.GetRequiredService<MapSessionService>());

var botToken = builder.Configuration["Telegram:BotToken"];

if (!string.IsNullOrWhiteSpace(botToken))
    builder.Services.AddOptinullTelegram(botToken);

var app = builder.Build();

if (string.IsNullOrWhiteSpace(botToken))
{
    app.Logger.LogWarning(
        "Telegram:BotToken is not configured, so the bot is off and no map link can be created. " +
        "Use 'dotnet user-secrets' or the Telegram__BotToken environment variable.");
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<MapSessionMiddleware>();

// Everything under /api needs a session; the middleware has already checked it.
var api = app.MapGroup("/api");

api.MapGet(
    "/config",
    (HttpContext http, MapSessionService sessions) =>
        Results.Ok(new { map, limits, session = sessions.Describe(MapSessionMiddleware.SessionOf(http)) }));

api.MapPost(
    "/vrp/validate",
    (GeoVrpRequest request, GeoVrpValidator validator) => Results.Ok(validator.Validate(request)));

api.MapPost(
    "/vrp/solve",
    async (
        HttpContext http,
        GeoVrpRequest request,
        GeoVrpValidator validator,
        MapSessionService sessions,
        IRoutingClient routing,
        ILogger<Program> logger) =>
    {
        var session = MapSessionMiddleware.SessionOf(http);
        var validation = validator.Validate(request);

        if (!validation.Valid)
            return Results.Json(validation, statusCode: StatusCodes.Status400BadRequest);

        // Cheap check first, so a second press of Solve does not call the routing service again.
        if (sessions.IsBusy(session))
            return Error(StatusCodes.Status409Conflict, busyMessage);

        // The validator accepted the request, so every value it covers is present.
        var depot = new GeoPoint(request.Depot!.Latitude!.Value, request.Depot.Longitude!.Value);

        var customers = request.Customers!
            .Select(customer => new GeoPoint(customer!.Latitude!.Value, customer.Longitude!.Value))
            .ToList();

        VrpCostMatrix costs;

        try
        {
            costs = await routing.GetCostMatrixAsync(depot, customers, http.RequestAborted);
        }
        catch (UnreachableRouteException unreachable)
        {
            // The message names the two places; it never contains anything from the service.
            return Error(
                StatusCodes.Status422UnprocessableEntity,
                unreachable.Message + " Move one of the pins onto a connected road.");
        }
        catch (RoutingException failure)
        {
            logger.LogWarning(failure, "The routing service could not build the distance matrix.");

            return Error(
                StatusCodes.Status502BadGateway,
                "The routing service could not calculate road distances right now. Please try again in a moment.");
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            return Results.Empty;
        }

        VrpProblem problem;

        try
        {
            // Longitude is x and latitude is y, so the problem still carries where its places are.
            problem = new VrpProblem(
                depot.Longitude,
                depot.Latitude,
                request.Customers!.Select(customer => new VrpCustomer(
                    customer!.Longitude!.Value,
                    customer.Latitude!.Value,
                    (int)customer.Demand!.Value)),
                (int)request.Vehicles!.Value,
                (int)request.Capacity!.Value,
                costs);
        }
        catch (ArgumentException invalid)
        {
            return Error(StatusCodes.Status400BadRequest, invalid.Message);
        }

        var submitted = sessions.Submit(session, problem);

        return submitted.Outcome switch
        {
            MapSubmitOutcome.Started =>
                Results.Json(sessions.GetStatus(session), statusCode: StatusCodes.Status202Accepted),

            MapSubmitOutcome.Busy =>
                Error(StatusCodes.Status409Conflict, busyMessage),

            _ => Error(
                StatusCodes.Status503ServiceUnavailable,
                "The server is busy right now. Please try again in a moment.")
        };
    });

api.MapGet(
    "/vrp/status",
    (HttpContext http, MapSessionService sessions) =>
        Results.Ok(sessions.GetStatus(MapSessionMiddleware.SessionOf(http))));

// The roads are asked for separately from the solution, so a router failure here cannot cost
// the user the optimization result: the status endpoint still has every route.
api.MapGet(
    "/vrp/geometry",
    async (
        HttpContext http,
        MapSessionService sessions,
        IRoutingClient routing,
        ILogger<Program> logger) =>
    {
        var plan = sessions.GetRoutePlan(MapSessionMiddleware.SessionOf(http));

        if (plan is null)
            return Error(StatusCodes.Status409Conflict, "There is no finished optimization to draw.");

        var routes = new List<GeoRouteGeometry>(plan.Routes.Count);

        // One request per truck, one after another, to go easy on the routing service.
        for (var index = 0; index < plan.Routes.Count; index++)
        {
            var stops = plan.Routes[index]
                .Select(stop => new GeoPoint(stop.Latitude, stop.Longitude))
                .ToList();

            try
            {
                var road = await routing.GetRouteAsync(stops, http.RequestAborted);

                routes.Add(new GeoRouteGeometry(
                    index,
                    road.Points.Select(point => new[] { point.Latitude, point.Longitude }).ToList(),
                    road.DistanceMeters,
                    road.DurationSeconds,
                    null));
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (Exception failure) when (failure is RoutingException or ArgumentException)
            {
                logger.LogWarning(failure, "No road geometry for route {Route}.", index);

                routes.Add(new GeoRouteGeometry(
                    index,
                    null,
                    null,
                    null,
                    "The road for this truck could not be loaded."));
            }
        }

        return Results.Ok(new GeoGeometryResponse(plan.JobId, routes));
    });

api.MapPost(
    "/vrp/cancel",
    (HttpContext http, MapSessionService sessions) =>
    {
        var session = MapSessionMiddleware.SessionOf(http);

        sessions.CancelJob(session);

        return Results.Ok(sessions.GetStatus(session));
    });

app.Run();

static IResult Error(int status, string message) =>
    Results.Json(new ApiError(message), statusCode: status);

// Lets integration tests host the app.
public partial class Program { }
