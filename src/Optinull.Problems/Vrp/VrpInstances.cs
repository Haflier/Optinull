namespace Optinull.Problems.Vrp;

public static class VrpInstances
{
    /// <summary>
    /// Random customers in a 100 x 100 square around a central depot, with demands from 1 to 20.
    /// The capacity gives the vehicles an average load of about 80%, and is raised if the loads
    /// would not pack into that many vehicles.
    /// </summary>
    public static VrpProblem Random(int customerCount, int vehicles, int seed)
    {
        if (customerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(customerCount));

        if (vehicles <= 0)
            throw new ArgumentOutOfRangeException(nameof(vehicles));

        var random = new Random(seed);

        var customers = Enumerable
            .Range(0, customerCount)
            .Select(_ => new VrpCustomer(random.Next(0, 101), random.Next(0, 101), random.Next(1, 21)))
            .ToList();

        var total = customers.Sum(customer => (long)customer.Demand);
        var largest = customers.Max(customer => customer.Demand);

        var capacity = (int)Math.Max(Math.Ceiling(total / (vehicles * 0.8)), largest);

        while (VrpProblem.RequiredVehicles(customers.Select(customer => customer.Demand), capacity) > vehicles)
            capacity++;

        return new VrpProblem(50, 50, customers, vehicles, capacity);
    }
}
