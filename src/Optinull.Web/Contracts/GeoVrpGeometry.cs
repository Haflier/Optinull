namespace Optinull.Web.Contracts;

/// <summary>
/// The road a truck drives. Points are [latitude, longitude] pairs from the depot and back. When
/// the router could not provide the road, Points is null and Error says so; the truck's route
/// itself is still valid, only its drawing is missing.
/// </summary>
public sealed record GeoRouteGeometry(
    int Route,
    IReadOnlyList<double[]>? Points,
    double? DistanceMeters,
    double? DurationSeconds,
    string? Error);

public sealed record GeoGeometryResponse(long JobId, IReadOnlyList<GeoRouteGeometry> Routes);
