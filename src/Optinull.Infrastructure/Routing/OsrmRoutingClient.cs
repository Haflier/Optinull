using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Optinull.Application.Routing;
using Optinull.Problems.Vrp;

namespace Optinull.Infrastructure.Routing;

public sealed class OsrmRoutingClient : IRoutingClient
{
    private readonly HttpClient _http;
    private readonly OsrmOptions _options;
    private readonly ConcurrentDictionary<string, VrpCostMatrix> _matrices = new();

    public OsrmRoutingClient(HttpClient http, OsrmOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(http);

        _http = http;
        _options = options ?? new OsrmOptions();
    }

    public int MaxLocations => _options.MaxLocations;

    public async Task<VrpCostMatrix> GetCostMatrixAsync(
        GeoPoint depot,
        IReadOnlyList<GeoPoint> customers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customers);

        if (customers.Count == 0)
            throw new ArgumentException("At least one customer is needed.", nameof(customers));

        var points = new List<GeoPoint>(customers.Count + 1) { depot };
        points.AddRange(customers);

        if (points.Count > _options.MaxLocations)
        {
            throw new ArgumentException(
                $"{points.Count} locations is more than the {_options.MaxLocations} allowed.",
                nameof(customers));
        }

        foreach (var point in points)
            Validate(point);

        var coordinates = Format(points);

        if (_matrices.TryGetValue(coordinates, out var cached))
            return cached;

        using var document = await GetAsync(
            $"table/v1/{_options.Profile}/{coordinates}?annotations=distance",
            cancellationToken).ConfigureAwait(false);

        var matrix = ParseMatrix(document.RootElement, points.Count);

        if (_matrices.Count >= _options.CacheEntries)
            _matrices.Clear();

        _matrices[coordinates] = matrix;

        return matrix;
    }

    public async Task<RoadRoute> GetRouteAsync(
        IReadOnlyList<GeoPoint> stops,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stops);

        if (stops.Count < 2)
            throw new ArgumentException("A route needs at least two stops.", nameof(stops));

        if (stops.Count > _options.MaxLocations)
        {
            throw new ArgumentException(
                $"{stops.Count} stops is more than the {_options.MaxLocations} allowed.",
                nameof(stops));
        }

        foreach (var stop in stops)
            Validate(stop);

        using var document = await GetAsync(
            $"route/v1/{_options.Profile}/{Format(stops)}?overview=full&geometries=geojson",
            cancellationToken).ConfigureAwait(false);

        return ParseRoute(document.RootElement);
    }

    // ---- Parsing -----------------------------------------------------------------------------

    private static VrpCostMatrix ParseMatrix(JsonElement root, int size)
    {
        EnsureOk(root);

        if (!root.TryGetProperty("distances", out var rows) ||
            rows.ValueKind != JsonValueKind.Array ||
            rows.GetArrayLength() != size)
        {
            throw new RoutingException("The routing service returned no distance matrix of the expected size.");
        }

        var costs = new double[size, size];
        var a = 0;

        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != size)
                throw new RoutingException("The routing service returned a malformed distance matrix.");

            var b = 0;

            foreach (var cell in row.EnumerateArray())
            {
                // OSRM reports a pair it cannot connect as null. That is not a free trip.
                if (cell.ValueKind == JsonValueKind.Null)
                    throw new UnreachableRouteException(a, b);

                if (cell.ValueKind != JsonValueKind.Number)
                    throw new RoutingException("The routing service returned a malformed distance matrix.");

                costs[a, b] = cell.GetDouble();
                b++;
            }

            a++;
        }

        try
        {
            return new VrpCostMatrix(costs);
        }
        catch (ArgumentException error)
        {
            throw new RoutingException("The routing service returned unusable distances: " + error.Message, error);
        }
    }

    private static RoadRoute ParseRoute(JsonElement root)
    {
        EnsureOk(root);

        try
        {
            var route = root.GetProperty("routes")[0];

            var points = route
                .GetProperty("geometry")
                .GetProperty("coordinates")
                .EnumerateArray()
                .Select(c => new GeoPoint(Latitude: c[1].GetDouble(), Longitude: c[0].GetDouble()))
                .ToList();

            if (points.Count == 0)
                throw new RoutingException("The routing service returned an empty route.");

            return new RoadRoute(
                points,
                route.GetProperty("distance").GetDouble(),
                route.GetProperty("duration").GetDouble());
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            throw new RoutingException("The routing service returned a malformed route.", error);
        }
    }

    private static void EnsureOk(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("code", out var code) &&
            code.ValueKind == JsonValueKind.String &&
            code.GetString() == "Ok")
        {
            return;
        }

        throw new RoutingException($"The routing service reported: {Describe(root)}");
    }

    private static string Describe(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return "an unexpected answer";

        var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        var message = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

        return string.Join(" - ", new[] { code, message }.Where(part => !string.IsNullOrEmpty(part))) is { Length: > 0 } text
            ? text
            : "an unexpected answer";
    }

    // ---- HTTP --------------------------------------------------------------------------------

    private async Task<JsonDocument> GetAsync(string path, CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{path}";

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(_options.Timeout);

        HttpStatusCode status;
        string body;

        try
        {
            using var response = await _http.GetAsync(url, linked.Token).ConfigureAwait(false);

            status = response.StatusCode;
            body = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RoutingException(
                $"The routing service timed out after {_options.Timeout.TotalSeconds:0.#} seconds.");
        }
        catch (HttpRequestException error)
        {
            throw new RoutingException("The routing service could not be reached: " + error.Message, error);
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException error)
        {
            throw new RoutingException(
                $"The routing service answered {(int)status} with something that is not JSON.", error);
        }

        if ((int)status is < 200 or > 299)
        {
            using (document)
            {
                throw new RoutingException(
                    $"The routing service answered {(int)status}: {Describe(document.RootElement)}");
            }
        }

        return document;
    }

    // ---- Coordinates -------------------------------------------------------------------------

    private static void Validate(GeoPoint point)
    {
        if (!double.IsFinite(point.Latitude) || !double.IsFinite(point.Longitude) ||
            point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180)
        {
            throw new ArgumentException(
                $"({point.Latitude}, {point.Longitude}) is not a valid latitude and longitude.");
        }
    }

    // OSRM wants longitude,latitude pairs separated by semicolons.
    private static string Format(IEnumerable<GeoPoint> points) =>
        string.Join(
            ';',
            points.Select(p =>
                p.Longitude.ToString("0.######", CultureInfo.InvariantCulture) + "," +
                p.Latitude.ToString("0.######", CultureInfo.InvariantCulture)));
}
