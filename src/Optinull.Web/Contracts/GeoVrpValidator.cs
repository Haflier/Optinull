using System.Globalization;
using Optinull.Problems.Vrp;

namespace Optinull.Web.Contracts;

/// <summary>One problem with the request. Field names the input: "depot", "vehicles", "customers[2].demand".</summary>
public sealed record GeoVrpIssue(string Field, string Message);

public sealed record GeoVrpSummary(int Customers, long TotalDemand, int MinVehicles);

public sealed record GeoVrpValidation(bool Valid, IReadOnlyList<GeoVrpIssue> Errors, GeoVrpSummary? Summary);

/// <summary>
/// Checks a map problem before it goes anywhere near the router or a solver. The browser shows
/// these messages, but the server is the authority: nothing the page checks is trusted.
/// </summary>
public sealed class GeoVrpValidator(GeoVrpLimits limits)
{
    public GeoVrpValidation Validate(GeoVrpRequest? request)
    {
        var errors = new List<GeoVrpIssue>();

        if (request is null)
        {
            errors.Add(new GeoVrpIssue("request", "The request is empty."));

            return new GeoVrpValidation(false, errors, null);
        }

        CheckDepot(request.Depot, errors);

        var hasVehicles = TryWhole(request.Vehicles, 1, limits.MaxVehicles, out var vehicles);

        if (!hasVehicles)
        {
            errors.Add(new GeoVrpIssue(
                "vehicles",
                $"The number of trucks must be a whole number from 1 to {limits.MaxVehicles}."));
        }

        var hasCapacity = TryWhole(request.Capacity, 1, limits.MaxCapacity, out var capacity);

        if (!hasCapacity)
        {
            errors.Add(new GeoVrpIssue(
                "capacity",
                $"The truck capacity must be a whole number from 1 to {limits.MaxCapacity}."));
        }

        var demands = CheckCustomers(request.Customers, hasCapacity ? capacity : null, errors);

        if (hasVehicles && hasCapacity && demands is not null)
        {
            var required = VrpProblem.RequiredVehicles(demands, capacity);

            if (vehicles < required)
            {
                errors.Add(new GeoVrpIssue(
                    "vehicles",
                    $"The loads need at least {required} trucks of capacity {capacity}, " +
                    $"but only {vehicles} are available."));
            }
        }

        if (errors.Count > 0)
            return new GeoVrpValidation(false, errors, null);

        var summary = new GeoVrpSummary(
            demands!.Count,
            demands.Sum(demand => (long)demand),
            VrpProblem.RequiredVehicles(demands, capacity));

        return new GeoVrpValidation(true, errors, summary);
    }

    private static void CheckDepot(GeoPointDto? depot, List<GeoVrpIssue> errors)
    {
        if (depot is null)
        {
            errors.Add(new GeoVrpIssue("depot", "Place the depot on the map."));

            return;
        }

        if (!IsCoordinate(depot.Latitude, depot.Longitude))
            errors.Add(new GeoVrpIssue("depot", "The depot is not a valid latitude and longitude."));
    }

    // Returns the demands when every customer is usable, so the fleet check can run on them.
    private List<int>? CheckCustomers(
        IReadOnlyList<GeoCustomerDto?>? customers,
        int? capacity,
        List<GeoVrpIssue> errors)
    {
        if (customers is null || customers.Count == 0)
        {
            errors.Add(new GeoVrpIssue("customers", "Add at least one customer."));

            return null;
        }

        if (customers.Count > limits.MaxCustomers)
        {
            errors.Add(new GeoVrpIssue(
                "customers",
                $"{customers.Count} customers is more than the {limits.MaxCustomers} allowed."));

            return null;
        }

        var demands = new List<int>(customers.Count);
        var usable = true;

        for (var i = 0; i < customers.Count; i++)
        {
            var number = i + 1;
            var customer = customers[i];

            if (customer is null)
            {
                errors.Add(new GeoVrpIssue($"customers[{i}]", $"Customer {number} is missing."));
                usable = false;
                continue;
            }

            if (!IsCoordinate(customer.Latitude, customer.Longitude))
            {
                errors.Add(new GeoVrpIssue(
                    $"customers[{i}]",
                    $"Customer {number} is not a valid latitude and longitude."));
                usable = false;
            }

            var largest = capacity ?? limits.MaxCapacity;

            if (!TryWhole(customer.Demand, 1, largest, out var demand))
            {
                errors.Add(new GeoVrpIssue($"customers[{i}].demand", DemandMessage(number, customer.Demand, capacity)));
                usable = false;
                continue;
            }

            demands.Add(demand);
        }

        return usable ? demands : null;
    }

    private static string DemandMessage(int number, double? demand, int? capacity)
    {
        // A whole, positive demand that is simply too big deserves its own wording.
        if (demand is { } value && double.IsFinite(value) && value == Math.Floor(value) && value > 0 && capacity is { } cap)
        {
            return $"Customer {number}: a demand of {value.ToString("0", CultureInfo.InvariantCulture)} " +
                   $"is more than the truck capacity {cap}.";
        }

        return $"Customer {number}: the demand must be a whole number of at least 1" +
               (capacity is { } c ? $" and at most the truck capacity {c}." : ".");
    }

    private static bool IsCoordinate(double? latitude, double? longitude) =>
        latitude is { } lat && double.IsFinite(lat) && lat is >= -90 and <= 90 &&
        longitude is { } lon && double.IsFinite(lon) && lon is >= -180 and <= 180;

    private static bool TryWhole(double? value, int min, int max, out int result)
    {
        result = 0;

        if (value is not { } number || !double.IsFinite(number) || number != Math.Floor(number))
            return false;

        if (number < min || number > max)
            return false;

        result = (int)number;

        return true;
    }
}
