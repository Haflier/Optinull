using Optinull.Application.Maps;

namespace Optinull.Application.Tests.Maps;

public sealed class MapVrpDialogTests
{
    private static MapVrpDialog Create(int maxLocations = 100) => new(maxLocations);

    [Fact]
    public void TheDepotComesFirst_ThenCustomers_WithDemandOneByDefault()
    {
        var dialog = Create();

        dialog.AddLocation(52.5, 13.4);
        var reply = dialog.AddLocation(52.51, 13.41);

        Assert.Contains("Customer 1 added, demand 1", reply.Text);
        Assert.Equal(MapPhase.Customers, dialog.Phase);
    }

    [Fact]
    public void ANumberRightAfterACustomer_SetsItsDemand()
    {
        var dialog = Create();

        dialog.AddLocation(52.5, 13.4);
        dialog.AddLocation(52.51, 13.41);
        var reply = dialog.AddText("5");

        Assert.Contains("demand set to 5", reply.Text);

        dialog.Done();
        dialog.AddText("1 5");

        Assert.True(dialog.TryBuild(out var input, out _));
        Assert.Equal(new[] { 5 }, input!.Demands);
    }

    [Fact]
    public void ANumber_BeforeAnyCustomer_IsRefused()
    {
        var dialog = Create();

        dialog.AddLocation(52.5, 13.4);

        Assert.Contains("customer location first", dialog.AddText("3").Text);
    }

    [Fact]
    public void PastedLines_AddSeveralCustomers_WithOptionalDemands()
    {
        var dialog = Create();

        dialog.AddText("52.5, 13.4\n52.51, 13.41\n52.52 13.42 4\n52.53; 13.43");
        dialog.Done();
        dialog.AddText("2 10");

        Assert.True(dialog.TryBuild(out var input, out _));
        Assert.Equal(3, input!.Customers.Count);
        Assert.Equal(new[] { 1, 4, 1 }, input.Demands);
        Assert.Equal(52.5, input.Depot.Latitude);
        Assert.Equal(13.43, input.Customers[2].Longitude);
    }

    [Fact]
    public void ABadLine_StopsThePaste_AndSaysWhy()
    {
        var dialog = Create();

        var reply = dialog.AddText("52.5, 13.4\n95, 13.41\n52.52, 13.42");

        Assert.Contains("not used", reply.Text);
        Assert.Equal(MapPhase.Customers, dialog.Phase);
        Assert.DoesNotContain("Customer 1 added", reply.Text);
    }

    [Fact]
    public void Done_NeedsACustomer()
    {
        var dialog = Create();

        dialog.AddLocation(52.5, 13.4);

        Assert.Contains("at least one customer", dialog.Done().Text);
        Assert.Equal(MapPhase.Customers, dialog.Phase);
    }

    [Fact]
    public void TheFleet_MustFitTheLoads()
    {
        var dialog = Create();

        dialog.AddText("52.5, 13.4\n52.51, 13.41 8\n52.52, 13.42 8\ndone");

        Assert.Contains("more than the capacity", dialog.AddText("2 5").Text);
        Assert.Contains("at least 2 trucks", dialog.AddText("1 10").Text);
        Assert.Equal(MapPhase.Fleet, dialog.Phase);

        Assert.Contains("Press Solve", dialog.AddText("2 10").Text);
        Assert.Equal(MapPhase.Ready, dialog.Phase);
    }

    [Fact]
    public void ANewLocation_AfterTheFleet_GoesBackToCustomers()
    {
        var dialog = Create();

        dialog.AddText("52.5, 13.4\n52.51, 13.41\ndone\n1 5");
        dialog.AddLocation(52.52, 13.42);

        Assert.Equal(MapPhase.Customers, dialog.Phase);
        Assert.False(dialog.TryBuild(out _, out var error));
        Assert.Contains("not complete", error);
    }

    [Fact]
    public void Undo_RemovesTheLastCustomer_ThenTheDepot()
    {
        var dialog = Create();

        dialog.AddText("52.5, 13.4\n52.51, 13.41");

        Assert.Contains("Removed customer 1", dialog.Undo().Text);
        Assert.Contains("Removed the depot", dialog.Undo().Text);
        Assert.Equal(MapPhase.Depot, dialog.Phase);
        Assert.Contains("nothing to undo", dialog.Undo().Text);
    }

    [Fact]
    public void TheCustomerLimit_FollowsTheRoutingServicesLocationLimit()
    {
        var dialog = Create(maxLocations: 3);

        dialog.AddText("52.5, 13.4\n52.51, 13.41\n52.52, 13.42");

        Assert.Contains("maximum of 2 customers", dialog.AddLocation(52.53, 13.43).Text);
    }

    [Fact]
    public void AnInvalidLocation_IsRefused()
    {
        var dialog = Create();

        Assert.Contains("not a valid", dialog.AddLocation(123, 13.4).Text);
        Assert.Equal(MapPhase.Depot, dialog.Phase);
    }
}
