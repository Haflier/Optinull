using Optinull.Infrastructure.Routing;
using Optinull.Problems.Vrp;

namespace Optinull.Web.Contracts;

/// <summary>
/// What a map problem may ask for. Customers are capped by the smaller of the routing service's
/// location limit (which includes the depot) and the existing routing input limits.
/// </summary>
public sealed record GeoVrpLimits(int MaxCustomers, int MaxVehicles, int MaxCapacity)
{
    public static GeoVrpLimits Default { get; } = From(VrpInputLimits.Default, new OsrmOptions());

    public static GeoVrpLimits From(VrpInputLimits routing, OsrmOptions osrm)
    {
        ArgumentNullException.ThrowIfNull(routing);
        ArgumentNullException.ThrowIfNull(osrm);

        return new GeoVrpLimits(
            Math.Min(routing.MaxCustomers, osrm.MaxLocations - 1),
            routing.MaxVehicles,
            routing.MaxCapacity);
    }
}
