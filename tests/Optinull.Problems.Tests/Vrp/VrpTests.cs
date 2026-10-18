using Optinull.Optimization.Randomness;
using Optinull.Problems.Vrp;

namespace Optinull.Problems.Tests.Vrp;

public sealed class VrpTests
{
    // Depot at the origin; customers on a line so every distance is easy to check by hand.
    private static VrpProblem Line(int vehicles = 2, int capacity = 10) =>
        new(0, 0, [new(1, 0, 4), new(2, 0, 4), new(3, 0, 4), new(4, 0, 4)], vehicles, capacity);

    private static double Distance(VrpProblem problem, IReadOnlyList<IReadOnlyList<int>> routes)
    {
        double total = 0;

        foreach (var route in routes)
        {
            double x = problem.DepotX, y = problem.DepotY;

            foreach (var customer in route)
            {
                var c = problem.Customers[customer];
                total += Math.Sqrt(Math.Pow(c.X - x, 2) + Math.Pow(c.Y - y, 2));
                (x, y) = (c.X, c.Y);
            }

            total += Math.Sqrt(Math.Pow(problem.DepotX - x, 2) + Math.Pow(problem.DepotY - y, 2));
        }

        return total;
    }

    // ---- The problem ------------------------------------------------------------------------

    [Fact]
    public void Problem_RejectsBadInput()
    {
        VrpCustomer[] one = [new(1, 1, 5)];

        Assert.Throws<ArgumentException>(() => new VrpProblem(0, 0, [], 1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VrpProblem(0, 0, one, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VrpProblem(0, 0, one, 1, 0));
        Assert.Throws<ArgumentException>(() => new VrpProblem(0, 0, [new(1, 1, 0)], 1, 10));
        Assert.Throws<ArgumentException>(() => new VrpProblem(0, 0, [new(1, 1, 11)], 1, 10));
        Assert.Throws<ArgumentException>(() => new VrpProblem(double.NaN, 0, one, 1, 10));
        Assert.Throws<ArgumentException>(() => new VrpProblem(0, 0, [new(double.PositiveInfinity, 0, 1)], 1, 10));
    }

    [Fact]
    public void Problem_RefusesAFleetTooSmallForTheLoads()
    {
        // Four loads of 4 in vehicles of 10 need two vehicles (4 + 4 each).
        var error = Assert.Throws<ArgumentException>(() => Line(vehicles: 1));

        Assert.Contains("at least 2 vehicles", error.Message);
    }

    [Theory]
    [InlineData(new[] { 4, 4, 4, 4 }, 10, 2)]
    [InlineData(new[] { 6, 6, 6 }, 10, 3)]
    [InlineData(new[] { 5, 5, 5, 5 }, 10, 2)]
    [InlineData(new[] { 10 }, 10, 1)]
    [InlineData(new[] { 7, 3, 6, 4, 5, 5 }, 10, 3)]
    public void RequiredVehicles_IsAFleetThatCertainlyFits(int[] demands, int capacity, int expected) =>
        Assert.Equal(expected, VrpProblem.RequiredVehicles(demands, capacity));

    [Fact]
    public void BinPacking_PutsEveryLoadInOneBin_WithoutOverfillingAny()
    {
        var demands = new[] { 7, 3, 6, 4, 5, 5, 2, 8 };
        var bins = VrpBinPacking.FirstFitDecreasing(demands, 10);

        Assert.Equal(Enumerable.Range(0, demands.Length), bins.SelectMany(bin => bin).OrderBy(i => i));
        Assert.All(bins, bin => Assert.True(bin.Sum(i => demands[i]) <= 10));
    }

    [Fact]
    public void Distances_AreEuclidean()
    {
        var problem = new VrpProblem(0, 0, [new(3, 4, 1), new(6, 8, 1)], 1, 10);

        Assert.Equal(5, problem.FromDepot(0), 9);
        Assert.Equal(10, problem.FromDepot(1), 9);
        Assert.Equal(5, problem.Distance(0, 1), 9);
        Assert.Equal(problem.Distance(0, 1), problem.Distance(1, 0), 9);
    }

    // ---- Evaluating a plan --------------------------------------------------------------------

    [Fact]
    public void Evaluate_MatchesAnIndependentDistance()
    {
        var problem = VrpInstances.Random(20, 4, seed: 3);
        var plan = new VrpSearchProblem(problem).CreateRandom(new RandomSource(1));

        var result = VrpEvaluator.Evaluate(problem, plan);

        Assert.True(result.IsFeasible);
        Assert.Equal(Distance(problem, plan.Routes), result.ObjectiveValue, 6);
    }

    [Fact]
    public void Evaluate_OfAHandBuiltPlan_IsTheSumOfItsRoutes()
    {
        var problem = Line();

        // 0 -> 1 -> 2 -> 0 is 1 + 1 + 2 = 4, and 0 -> 3 -> 4 -> 0 is 3 + 1 + 4 = 8.
        var plan = new VrpPlan([[0, 1], [2, 3]]);

        Assert.Equal(4 + 8, VrpEvaluator.Evaluate(problem, plan).ObjectiveValue, 9);
        Assert.Equal(8, VrpEvaluator.RouteLoad(problem, plan.Routes[0]));
    }

    [Fact]
    public void Evaluate_FlagsAnOverloadedVehicle_AndTooManyRoutes()
    {
        var problem = Line();

        var overloaded = VrpEvaluator.Evaluate(problem, new VrpPlan([[0, 1, 2], [3]]));

        Assert.False(overloaded.IsFeasible);
        Assert.Equal(2, overloaded.Penalty);

        var tooMany = VrpEvaluator.Evaluate(problem, new VrpPlan([[0], [1], [2, 3]]));

        Assert.False(tooMany.IsFeasible);
        Assert.True(tooMany.Penalty > 0);
    }

    [Fact]
    public void Evaluate_RefusesAPlanThatSkipsOrRepeatsACustomer()
    {
        var problem = Line();

        Assert.Throws<ArgumentException>(() => VrpEvaluator.Evaluate(problem, new VrpPlan([[0, 1], [2]])));
        Assert.Throws<ArgumentException>(() => VrpEvaluator.Evaluate(problem, new VrpPlan([[0, 1], [1, 2, 3]])));
        Assert.Throws<ArgumentException>(() => VrpEvaluator.Evaluate(problem, new VrpPlan([[0, 1], [2, 9]])));
    }

    [Fact]
    public void APlan_DropsEmptyRoutes_AndNeedsOneRoute()
    {
        Assert.Equal(2, new VrpPlan([[0], [], [1]]).RouteCount);
        Assert.Throws<ArgumentException>(() => new VrpPlan([[], []]));
    }

    // ---- Cutting an order into routes -----------------------------------------------------------

    // The cheapest way to cut an order into at most `vehicles` consecutive runs, by trying every cut.
    private static double BruteForceSplit(VrpProblem problem, int[] order)
    {
        var n = order.Length;
        var best = double.PositiveInfinity;

        for (var mask = 0; mask < 1 << (n - 1); mask++)
        {
            var routes = new List<List<int>> { new() { order[0] } };

            for (var i = 1; i < n; i++)
            {
                if ((mask & (1 << (i - 1))) != 0)
                    routes.Add([]);

                routes[^1].Add(order[i]);
            }

            if (routes.Count > problem.Vehicles ||
                routes.Any(route => route.Sum(c => problem.Customers[c].Demand) > problem.Capacity))
            {
                continue;
            }

            best = Math.Min(best, routes.Sum(route => VrpEvaluator.RouteDistance(problem, route)));
        }

        return best;
    }

    [Fact]
    public void Split_IsTheCheapestPossibleCut_OnManyRandomOrders()
    {
        var random = new Random(5);

        for (var round = 0; round < 40; round++)
        {
            var problem = VrpInstances.Random(8, 3 + round % 3, seed: round);
            var order = Enumerable.Range(0, 8).OrderBy(_ => random.Next()).ToArray();

            var plan = VrpSplitter.Split(problem, order);
            var expected = BruteForceSplit(problem, order);

            if (double.IsPositiveInfinity(expected))
            {
                Assert.Null(plan);
                continue;
            }

            Assert.NotNull(plan);
            Assert.Equal(expected, VrpEvaluator.TotalDistance(problem, plan!), 6);
            Assert.True(VrpEvaluator.Evaluate(problem, plan!).IsFeasible);
            Assert.Equal(order, plan!.Routes.SelectMany(route => route).ToArray());
        }
    }

    [Fact]
    public void Split_ReturnsNull_WhenTheOrderCannotFitTheFleet()
    {
        // Loads 6, 5, 6, 5 fit two vehicles of 11 (6 + 5 each), but only in the order 6, 5, 6, 5.
        // In the order 6, 6, 5, 5 the consecutive runs need three vehicles.
        var problem = new VrpProblem(0, 0, [new(1, 0, 6), new(2, 0, 5), new(3, 0, 6), new(4, 0, 5)], 2, 11);

        Assert.NotNull(VrpSplitter.Split(problem, [0, 1, 2, 3]));
        Assert.Null(VrpSplitter.Split(problem, [0, 2, 1, 3]));
    }

    // ---- The search problem -----------------------------------------------------------------------

    private static VrpProblem TightFleet() =>
        // Loads 6 and 4 pair up exactly: two vehicles of capacity 10 and no spare room at all.
        new(0, 0, [new(1, 1, 6), new(5, 1, 4), new(1, 5, 6), new(5, 5, 4)], 2, 10);

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 5)]
    [InlineData(false, 8)]
    public void EverythingTheSearchBuilds_StaysFeasible_EvenWithAFleetThatIsBarelyEnough(bool tight, int vehicles)
    {
        var problem = tight
            ? TightFleet()
            : VrpInstances.Random(25, vehicles, seed: 9);
        var search = new VrpSearchProblem(problem);
        var random = new RandomSource(2);

        var current = search.CreateRandom(random);
        var other = search.CreateRandom(random);

        for (var i = 0; i < 400; i++)
        {
            Assert.True(search.Evaluate(current).IsFeasible, "a random plan broke a limit");
            Assert.True(search.Evaluate(other).IsFeasible, "a random plan broke a limit");

            current = search.RandomNeighbor(current, random);
            other = search.Mutate(search.Crossover(current, other, random), random);
        }

        Assert.True(search.Evaluate(search.CreateGreedy(search.Evaluate)).IsFeasible);
    }

    [Fact]
    public void Greedy_IsNearestNeighbour_AndNeverUsesMoreVehiclesThanItHas()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var problem = VrpInstances.Random(30, 3 + seed % 4, seed);
            var search = new VrpSearchProblem(problem);

            var greedy = search.CreateGreedy(search.Evaluate);
            var result = search.Evaluate(greedy);

            Assert.True(result.IsFeasible, $"seed {seed}");
            Assert.True(greedy.RouteCount <= problem.Vehicles);
        }
    }

    [Fact]
    public void Greedy_OnALine_VisitsTheNearestFirst()
    {
        var problem = Line(vehicles: 2, capacity: 8);
        var search = new VrpSearchProblem(problem);

        var plan = search.CreateGreedy(search.Evaluate);

        Assert.Equal([[0, 1], [2, 3]], plan.Routes.Select(route => route.ToArray()).ToArray());
    }

    [Fact]
    public void Neighbors_AreAllFeasible_AndDifferFromThePlan()
    {
        var problem = VrpInstances.Random(12, 4, seed: 2);
        var search = new VrpSearchProblem(problem);
        var start = search.CreateRandom(new RandomSource(4));

        var neighbors = search.Neighbors(start).ToList();

        Assert.NotEmpty(neighbors);

        foreach (var neighbor in neighbors)
        {
            Assert.True(search.Evaluate(neighbor).IsFeasible);
            Assert.False(
                neighbor.Routes.Select(route => string.Join(",", route)).SequenceEqual(
                    start.Routes.Select(route => string.Join(",", route))),
                "a neighbour was the plan itself");
        }
    }

    [Fact]
    public void Neighbors_CanOpenAnUnusedVehicle_AndCloseARoute()
    {
        var problem = new VrpProblem(0, 0, [new(10, 0, 1), new(-10, 0, 1)], vehicles: 2, capacity: 10);
        var search = new VrpSearchProblem(problem);

        // One vehicle serves both customers: a neighbour can send one of them out alone...
        var together = new VrpPlan([[0, 1]]);

        Assert.Contains(search.Neighbors(together), plan => plan.RouteCount == 2);

        // ...and from two routes a neighbour can fold one customer into the other.
        var apart = new VrpPlan([[0], [1]]);

        Assert.Contains(search.Neighbors(apart), plan => plan.RouteCount == 1);
    }

    [Fact]
    public void HillClimbingOverNeighbors_NeverGetsWorse_AndEndsFeasible()
    {
        var problem = VrpInstances.Random(14, 3, seed: 6);
        var search = new VrpSearchProblem(problem);

        var current = search.CreateRandom(new RandomSource(5));
        var distance = VrpEvaluator.TotalDistance(problem, current);
        var start = distance;

        while (true)
        {
            var better = search.Neighbors(current)
                .Select(plan => (Plan: plan, Distance: VrpEvaluator.TotalDistance(problem, plan)))
                .Where(candidate => candidate.Distance < distance - 1e-9)
                .OrderBy(candidate => candidate.Distance)
                .FirstOrDefault();

            if (better.Plan is null)
                break;

            current = better.Plan;
            distance = better.Distance;
        }

        Assert.True(distance <= start);
        Assert.True(search.Evaluate(current).IsFeasible);
    }

    [Fact]
    public void Crossover_KeepsEveryCustomerExactlyOnce()
    {
        var problem = VrpInstances.Random(30, 5, seed: 1);
        var search = new VrpSearchProblem(problem);
        var random = new RandomSource(8);

        var a = search.CreateRandom(random);
        var b = search.CreateRandom(random);

        for (var i = 0; i < 100; i++)
        {
            var child = search.Crossover(a, b, random);

            Assert.Equal(Enumerable.Range(0, 30), child.Routes.SelectMany(r => r).OrderBy(c => c));
            Assert.True(search.Evaluate(child).IsFeasible);

            a = b;
            b = child;
        }
    }

    [Fact]
    public void ASingleCustomer_HasNoMoves_ButStillAPlan()
    {
        var problem = new VrpProblem(0, 0, [new(5, 5, 1)], 1, 10);
        var search = new VrpSearchProblem(problem);
        var random = new RandomSource(1);

        var plan = search.CreateRandom(random);

        Assert.Same(plan, search.RandomNeighbor(plan, random));
        Assert.Empty(search.Neighbors(plan));
    }

    // ---- Random instances ------------------------------------------------------------------------------

    [Fact]
    public void RandomInstances_AreDeterministic_AndTheFleetAlwaysFits()
    {
        var first = VrpInstances.Random(25, 4, seed: 8);
        var second = VrpInstances.Random(25, 4, seed: 8);

        Assert.Equal(first.Customers, second.Customers);
        Assert.Equal(first.Capacity, second.Capacity);

        for (var seed = 0; seed < 30; seed++)
        {
            var problem = VrpInstances.Random(10 + seed, 1 + seed % 6, seed);

            Assert.True(
                VrpProblem.RequiredVehicles(problem.Customers.Select(c => c.Demand), problem.Capacity) <= problem.Vehicles);
            Assert.True(problem.Capacity >= problem.Customers.Max(c => c.Demand));
        }
    }

    [Fact]
    public void RandomInstances_GiveTheVehiclesRoomToSpare_WhenTheFleetIsGenerous()
    {
        var problem = VrpInstances.Random(40, 6, seed: 21);

        // About 80% full on average, so the loads fit with some slack.
        Assert.InRange(problem.TotalDemand / (double)(problem.Vehicles * problem.Capacity), 0.6, 0.9);
    }
}
