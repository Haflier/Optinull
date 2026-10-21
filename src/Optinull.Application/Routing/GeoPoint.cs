namespace Optinull.Application.Routing;

/// <summary>A place on Earth in degrees (WGS84).</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>A route along real roads: its geometry from start to end, and its totals.</summary>
public sealed record RoadRoute(IReadOnlyList<GeoPoint> Points, double DistanceMeters, double DurationSeconds);
