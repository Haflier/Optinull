using Optinull.Web.Contracts;

namespace Optinull.Integration.Tests.Web;

public sealed class GeoVrpValidatorTests
{
    private static readonly GeoVrpLimits Limits = new(MaxCustomers: 5, MaxVehicles: 4, MaxCapacity: 100);

    private static GeoVrpValidator Validator => new(Limits);

    private static GeoCustomerDto Customer(double? demand = 5, double? latitude = 52.51, double? longitude = 13.41) =>
        new(latitude, longitude, demand);

    private static GeoVrpRequest Valid(
        GeoPointDto? depot = null,
        IReadOnlyList<GeoCustomerDto?>? customers = null,
        double? vehicles = 2,
        double? capacity = 10) =>
        new(
            depot ?? new GeoPointDto(52.5, 13.4),
            customers ?? [Customer(4), Customer(6), Customer(5)],
            vehicles,
            capacity);

    [Fact]
    public void A_complete_problem_is_valid_and_summarised()
    {
        var result = Validator.Validate(Valid());

        Assert.True(result.Valid);
        Assert.Empty(result.Errors);
        Assert.Equal(new GeoVrpSummary(Customers: 3, TotalDemand: 15, MinVehicles: 2), result.Summary);
    }

    [Fact]
    public void A_missing_request_is_reported()
    {
        var result = Validator.Validate(null);

        Assert.False(result.Valid);
        Assert.Equal("request", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public void A_missing_depot_is_reported()
    {
        var request = new GeoVrpRequest(null, [Customer()], 1, 10);

        var result = Validator.Validate(request);

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Field == "depot");
    }

    [Theory]
    [InlineData(91.0, 13.4)]
    [InlineData(-91.0, 13.4)]
    [InlineData(52.5, 181.0)]
    [InlineData(52.5, -181.0)]
    [InlineData(double.NaN, 13.4)]
    [InlineData(52.5, double.PositiveInfinity)]
    public void A_depot_outside_the_globe_is_rejected(double latitude, double longitude)
    {
        var result = Validator.Validate(Valid(depot: new GeoPointDto(latitude, longitude)));

        Assert.False(result.Valid);
        Assert.Contains(result.Errors, e => e.Field == "depot");
    }

    [Fact]
    public void A_depot_with_a_missing_coordinate_is_rejected()
    {
        var result = Validator.Validate(Valid(depot: new GeoPointDto(52.5, null)));

        Assert.Contains(result.Errors, e => e.Field == "depot");
    }

    [Fact]
    public void No_customers_is_rejected()
    {
        var result = Validator.Validate(Valid(customers: []));

        Assert.Contains(result.Errors, e => e.Field == "customers");
    }

    [Fact]
    public void Too_many_customers_is_rejected()
    {
        var customers = Enumerable.Range(0, Limits.MaxCustomers + 1).Select(_ => (GeoCustomerDto?)Customer(1)).ToList();

        var result = Validator.Validate(Valid(customers: customers, vehicles: 1, capacity: 100));

        Assert.Contains(result.Errors, e => e.Field == "customers" && e.Message.Contains("5"));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(2.5)]
    [InlineData(double.NaN)]
    public void A_demand_that_is_not_a_positive_whole_number_names_the_customer(double demand)
    {
        var result = Validator.Validate(Valid(customers: [Customer(4), Customer(demand)]));

        Assert.False(result.Valid);

        var error = Assert.Single(result.Errors);

        Assert.Equal("customers[1].demand", error.Field);
        Assert.Contains("Customer 2", error.Message);
    }

    [Fact]
    public void A_missing_demand_is_rejected()
    {
        var result = Validator.Validate(Valid(customers: [Customer(demand: null)]));

        Assert.Contains(result.Errors, e => e.Field == "customers[0].demand");
    }

    [Fact]
    public void A_demand_above_the_capacity_is_rejected_with_a_specific_message()
    {
        var result = Validator.Validate(Valid(customers: [Customer(11)], vehicles: 4, capacity: 10));

        var error = Assert.Single(result.Errors);

        Assert.Equal("customers[0].demand", error.Field);
        Assert.Contains("capacity 10", error.Message);
    }

    [Fact]
    public void A_customer_outside_the_globe_is_rejected()
    {
        var result = Validator.Validate(Valid(customers: [Customer(latitude: 123)]));

        Assert.Contains(result.Errors, e => e.Field == "customers[0]");
    }

    [Fact]
    public void A_null_customer_entry_is_rejected()
    {
        var result = Validator.Validate(Valid(customers: [Customer(), null]));

        Assert.Contains(result.Errors, e => e.Field == "customers[1]");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(1.5)]
    public void The_number_of_trucks_must_be_whole_and_within_the_limit(double vehicles)
    {
        var result = Validator.Validate(Valid(vehicles: vehicles));

        Assert.Contains(result.Errors, e => e.Field == "vehicles");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(101.0)]
    [InlineData(7.5)]
    public void The_capacity_must_be_whole_and_within_the_limit(double capacity)
    {
        var result = Validator.Validate(Valid(capacity: capacity));

        Assert.Contains(result.Errors, e => e.Field == "capacity");
    }

    [Fact]
    public void Too_few_trucks_for_the_loads_is_rejected()
    {
        // 6 + 6 + 6 cannot share trucks of capacity 10, so three are needed.
        var result = Validator.Validate(Valid(customers: [Customer(6), Customer(6), Customer(6)], vehicles: 2, capacity: 10));

        var error = Assert.Single(result.Errors);

        Assert.Equal("vehicles", error.Field);
        Assert.Contains("at least 3", error.Message);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var request = new GeoVrpRequest(null, [Customer(demand: 0)], Vehicles: 0, Capacity: 10);

        var result = Validator.Validate(request);

        Assert.Equal(
            new[] { "customers[0].demand", "depot", "vehicles" },
            result.Errors.Select(e => e.Field).Order().ToArray());
    }

    [Fact]
    public void The_default_limits_respect_the_routing_service_location_cap()
    {
        // The public OSRM server accepts 100 locations including the depot.
        Assert.Equal(99, GeoVrpLimits.Default.MaxCustomers);
    }
}
