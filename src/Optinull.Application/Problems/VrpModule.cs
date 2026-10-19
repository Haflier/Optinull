using System.Diagnostics.CodeAnalysis;
using Optinull.Application.Optimization;
using Optinull.Application.Reporting;
using Optinull.Problems.Vrp;
using Optinull.Reporting.Charts;

namespace Optinull.Application.Problems;

public sealed class VrpModule : IMapProblemModule
{
    public string Id => "vrp";

    public string Title => "vehicle routing";

    public IReadOnlyList<string> BuiltInNames { get; } = ["vrp15", "vrp40"];

    public string CustomInputHelp
    {
        get
        {
            var limits = VrpInputLimits.Default;

            return
                "line 1: vehicles and capacity; line 2: the depot as 'x y'; then one customer per line " +
                "as 'x y demand'. Example:\n3 30\n50 50\n20 30 7\n80 65 12\n" +
                $"Limits: {limits.MaxCustomers} customers, {limits.MaxVehicles} vehicles.";
        }
    }

    public IReadOnlyList<ProblemParameter> Parameters { get; } =
    [
        new("customers", "number of random customers", 3, VrpInputLimits.Default.MaxCustomers, 20),
        new("vehicles", "number of vehicles", 1, VrpInputLimits.Default.MaxVehicles, 4)
    ];

    public IReadOnlyList<DatasetPreset> Presets { get; } =
    [
        new("small", "Small: 12 customers, 3 vehicles", "vrp",
            new Dictionary<string, string> { ["customers"] = "12", ["vehicles"] = "3" }),
        new("medium", "Medium: vrp40 (40 customers, 6 vehicles)", "vrp40"),
        new("large", "Large: 80 customers, 10 vehicles", "vrp",
            new Dictionary<string, string> { ["customers"] = "80", ["vehicles"] = "10" })
    ];

    public IProblemDialog CreateDialog() => VrpDialog.Create(this);

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        switch (name.Trim().ToLowerInvariant())
        {
            case "vrp15":
                instance = Describe("vrp15", VrpInstances.Random(15, 3, seed: 21));
                return true;

            case "vrp40":
                instance = Describe("vrp40", VrpInstances.Random(40, 6, seed: 21));
                return true;

            default:
                instance = null;
                return false;
        }
    }

    public bool TryParseCustom(
        string? text,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!VrpTextParser.TryParse(text, VrpInputLimits.Default, out var problem, out error))
            return false;

        instance = new VrpInstance(
            "custom",
            $"custom routing, {problem!.CustomerCount} customers, {problem.Vehicles} vehicles",
            problem);

        return true;
    }

    public bool TryGenerate(
        IReadOnlyDictionary<string, string> values,
        int seed,
        [NotNullWhen(true)] out IProblemInstance? instance,
        out string? error)
    {
        instance = null;

        if (!ProblemParameterReader.TryRead(Parameters, values, out var read, out error))
            return false;

        var customers = read["customers"];
        var vehicles = read["vehicles"];
        var problem = VrpInstances.Random(customers, vehicles, seed);

        instance = new VrpInstance(
            $"vrp{customers}v{vehicles}",
            $"random routing, {customers} customers, {vehicles} vehicles, capacity {problem.Capacity} (seed {seed})",
            problem);

        return true;
    }

    private static VrpInstance Describe(string name, VrpProblem problem) =>
        new(
            name,
            $"{name} ({problem.CustomerCount} customers, {problem.Vehicles} vehicles, capacity {problem.Capacity})",
            problem);
}

internal sealed class VrpInstance : IProblemInstance
{
    private readonly VrpProblem _problem;

    public VrpInstance(string name, string description, VrpProblem problem)
    {
        Name = name;
        Description = description;
        _problem = problem;
    }

    public string Name { get; }

    public string Description { get; }

    public string ScoreLabel => "Total distance";

    public bool HigherIsBetter => false;

    public SolverOutcome Run(
        SolverKind solver,
        SolveOptions options,
        CancellationToken cancellationToken)
    {
        var result = new VrpSolveService().Solve(
            _problem,
            solver,
            options.Seed,
            cancellationToken,
            options.Iterations,
            options.Progress,
            options.Tuning);

        return new SolverOutcome(
            solver,
            result.SolverName,
            result.Distance,
            result.Search.Elapsed,
            result.Search.EvaluationCount,
            ReportMapper.ToConvergenceSeries(result.SolverName, result.Search),
            () => RenderImages(result),
            Baseline: result.GreedyDistance,
            Settings: result.Settings);
    }

    private IReadOnlyList<ReportImage> RenderImages(VrpSolveResult result)
    {
        var customers = _problem.Customers
            .Select(customer => new RoutePoint(customer.X, customer.Y))
            .ToList();

        var routes = result.Plan.Routes
            .Select((route, index) => new VrpChartRoute(
                route,
                $"Route {index + 1}: load {VrpEvaluator.RouteLoad(_problem, route)}/{_problem.Capacity}"))
            .ToList();

        var map = new VrpChartRenderer().Render(
            new RoutePoint(_problem.DepotX, _problem.DepotY),
            customers,
            routes,
            $"{result.SolverName}: distance {result.Distance:0.0}, {result.Plan.RouteCount} routes");

        var convergence = new ConvergenceChartRenderer().Render(
            [ReportMapper.ToConvergenceSeries(result.SolverName, result.Search)],
            $"Convergence, {result.SolverName}");

        return
        [
            new ReportImage($"{Name}-routes.png", Caption(result), map),
            new ReportImage($"{Name}-convergence.png", "Convergence", convergence)
        ];
    }

    private string Caption(VrpSolveResult result)
    {
        var prefix = $"{Name} | {result.SolverName} | distance {result.Distance:0.0}, " +
                     $"{result.Plan.RouteCount} of {_problem.Vehicles} vehicles";

        var change = (result.GreedyDistance - result.Distance) / result.GreedyDistance;

        return $"{prefix} (nearest neighbour {result.GreedyDistance:0.0}, " +
               $"{Math.Abs(change):P1} {(change >= 0 ? "shorter" : "longer")})";
    }
}
