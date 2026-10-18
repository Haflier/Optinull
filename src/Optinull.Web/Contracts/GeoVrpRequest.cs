namespace Optinull.Web.Contracts;

// Numbers are nullable doubles on purpose: a missing field and a fractional demand such as 2.5
// both reach the validator, which can then say what is wrong. A strict int would make the JSON
// binder fail with a generic 400 before any message could be written.

/// <summary>A place on Earth in degrees (WGS84).</summary>
public sealed record GeoPointDto(double? Latitude, double? Longitude);

/// <summary>A customer location and how much it needs delivered.</summary>
public sealed record GeoCustomerDto(double? Latitude, double? Longitude, double? Demand);

/// <summary>
/// A routing problem drawn on a map: one depot, the customers, and a fleet of identical trucks.
/// This is its own structured request; it never goes through the text parser that reads
/// synthetic-coordinate instances.
/// </summary>
public sealed record GeoVrpRequest(
    GeoPointDto? Depot,
    IReadOnlyList<GeoCustomerDto?>? Customers,
    double? Vehicles,
    double? Capacity);

/// <summary>Where the map opens, from configuration.</summary>
public sealed class MapViewOptions
{
    public double Latitude { get; init; } = 20;

    public double Longitude { get; init; } = 0;

    public int Zoom { get; init; } = 2;
}
