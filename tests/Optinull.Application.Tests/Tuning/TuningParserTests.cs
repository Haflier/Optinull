using System.Globalization;
using Optinull.Application.Commands;
using Optinull.Application.Optimization;

namespace Optinull.Application.Tests.Tuning;

public sealed class TuningParserTests
{
    private static SolveCommand Solve(string text) =>
        Assert.IsType<SolveCommand>(BotCommandParser.Parse(text));

    private static string Invalid(string text) =>
        Assert.IsType<InvalidCommand>(BotCommandParser.Parse(text)).Message;

    [Fact]
    public void WithoutSettings_TuningIsNull() =>
        Assert.Null(Solve("/solve ft06 sa iterations=500").Tuning);

    [Fact]
    public void AnnealingSettings_AreParsed()
    {
        var tuning = Solve("/solve ft10 sa temperature=2.5 final=0.001 moves=30").Tuning!;

        Assert.Equal(2.5, tuning.Temperature);
        Assert.Equal(0.001, tuning.Final);
        Assert.Equal(30, tuning.Moves);
        Assert.Null(tuning.Population);
    }

    [Fact]
    public void GeneticSettings_AreParsed()
    {
        var tuning = Solve("/solve ft10 ga population=80 mutation=0.35 elite=4 tournament=5 localsearch=20").Tuning!;

        Assert.Equal(80, tuning.Population);
        Assert.Equal(0.35, tuning.Mutation);
        Assert.Equal(4, tuning.Elite);
        Assert.Equal(5, tuning.Tournament);
        Assert.Equal(20, tuning.LocalSearch);
    }

    [Fact]
    public void IteratedLocalSearchSetting_IsParsed() =>
        Assert.Equal(7, Solve("/solve ft10 ils perturbation=7").Tuning!.Perturbation);

    [Theory]
    [InlineData("/solve ft06 sa temp=2", "temperature")]
    [InlineData("/solve ft06 sa TEMP=2", "temperature")]
    [InlineData("/solve ft06 sa end=0.01", "final")]
    [InlineData("/solve ft06 ga pop=40", "population")]
    [InlineData("/solve ft06 ga mut=0.5", "mutation")]
    [InlineData("/solve ft06 ga ls=5", "localsearch")]
    [InlineData("/solve ft06 ils perturb=4", "perturbation")]
    public void ShortNames_AreAccepted(string text, string canonical) =>
        Assert.Contains(canonical, Solve(text).Tuning!.Describe());

    [Fact]
    public void Numbers_UseADotWhateverTheServerLocale()
    {
        var before = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal(0.25, Solve("/solve ft06 sa temperature=0.25").Tuning!.Temperature);
            Assert.Equal("temperature=0.25", Solve("/solve ft06 sa temperature=0.25").Tuning!.Describe());
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void ScientificNotation_IsAccepted() =>
        Assert.Equal(1e-6, Solve("/solve ft06 sa final=1e-6").Tuning!.Final);

    [Theory]
    [InlineData("/solve ft06 sa temperature=0", "temperature must be a number from 0.01 to 100")]
    [InlineData("/solve ft06 sa temperature=101", "temperature must be a number from 0.01 to 100")]
    [InlineData("/solve ft06 sa temperature=hot", "temperature must be a number")]
    [InlineData("/solve ft06 sa temperature=-1", "temperature must be a number")]
    [InlineData("/solve ft06 sa final=0.9", "final must be a number from 0.00000001 to 0.5")]
    [InlineData("/solve ft06 sa moves=0", "moves must be a whole number from 1 to 10000")]
    [InlineData("/solve ft06 sa moves=2.5", "moves must be a whole number")]
    [InlineData("/solve ft06 ga population=1", "population must be a whole number from 2 to 500")]
    [InlineData("/solve ft06 ga population=501", "population must be a whole number from 2 to 500")]
    [InlineData("/solve ft06 ga mutation=1.5", "mutation must be a number from 0 to 1")]
    [InlineData("/solve ft06 ga mutation=-0.1", "mutation must be a number")]
    [InlineData("/solve ft06 ga elite=101", "elite must be a whole number from 0 to 100")]
    [InlineData("/solve ft06 ga tournament=0", "tournament must be a whole number from 1 to 50")]
    [InlineData("/solve ft06 ga localsearch=201", "localsearch must be a whole number from 0 to 200")]
    [InlineData("/solve ft06 ils perturbation=0", "perturbation must be a whole number from 1 to 50")]
    public void OutOfRangeValues_AreExplained(string text, string message) =>
        Assert.Contains(message, Invalid(text));

    [Theory]
    [InlineData("/solve ft06 population=40", "population tunes Genetic Algorithm; add ga to the command.")]
    [InlineData("/solve ft06 perturbation=4", "perturbation tunes Iterated Local Search; add ils to the command.")]
    [InlineData("/solve ft06 sa population=40", "population tunes Genetic Algorithm, not Simulated Annealing.")]
    [InlineData("/solve ft06 ga temperature=2", "temperature tunes Simulated Annealing, not Genetic Algorithm.")]
    [InlineData("/solve ft06 ils moves=10", "moves tunes Simulated Annealing, not Iterated Local Search.")]
    [InlineData("/solve ft06 hc temperature=2", "Hill Climbing has no settings to tune.")]
    [InlineData("/solve ft06 greedy mutation=0.1", "Greedy has no settings to tune.")]
    public void SettingsForTheWrongSolver_AreExplained(string text, string message) =>
        Assert.Equal(message, Invalid(text));

    [Theory]
    [InlineData("/compare ft10 temperature=2 mutation=0.3 perturbation=5")]
    [InlineData("/solve ft10 all temperature=2 mutation=0.3 perturbation=5")]
    public void Compare_GivesEachSettingToItsOwnSolver(string text)
    {
        var command = Solve(text);

        Assert.True(command.CompareAll);
        Assert.Equal(2, command.Tuning!.Temperature);
        Assert.Equal(0.3, command.Tuning.Mutation);
        Assert.Equal(5, command.Tuning.Perturbation);
    }

    [Fact]
    public void Compare_StillChecksTheRanges() =>
        Assert.Contains("mutation must be", Invalid("/compare ft10 mutation=3"));

    [Fact]
    public void TuningKeys_AreNotMistakenForProblemSettings()
    {
        var command = Solve("/solve tsp cities=15 sa temp=2 seed=3 iterations=500");

        Assert.Equal("15", Assert.Single(command.ParameterValues).Value);
        Assert.Equal("cities", command.ParameterValues.Keys.Single());
        Assert.Equal(2, command.Tuning!.Temperature);
        Assert.Equal(3, command.Seed);
        Assert.Equal(500, command.Iterations);
    }

    [Theory]
    [InlineData("/solve ft06 sa temperature=2 temperature=3")]
    [InlineData("/solve ft06 sa temperature=2 temp=3")]
    public void AnySettingGivenTwice_IsRejected(string text) =>
        Assert.Contains("given twice", Invalid(text));

    [Fact]
    public void EveryCatalogKey_BelongsToASolverTheParserKnows()
    {
        foreach (var key in TuningCatalog.Keys)
        {
            var code = TuningCatalog.Code(key.Solver);
            var middle = key.Whole ? "1" : "0.5";
            var value = key.Min <= double.Parse(middle, CultureInfo.InvariantCulture) &&
                        double.Parse(middle, CultureInfo.InvariantCulture) <= key.Max
                ? middle
                : key.Min.ToString("0.########", CultureInfo.InvariantCulture);

            var command = Solve($"/solve ft06 {code} {key.Name}={value}");

            Assert.NotNull(command.Tuning);
            Assert.False(command.Tuning!.IsEmpty);
        }
    }

    [Fact]
    public void Describe_ListsOnlyWhatWasChanged_AndCanTagTheSolvers()
    {
        var tuning = new SolverTuning { Temperature = 2, Mutation = 0.3, Perturbation = 5 };

        Assert.Equal("temperature=2, mutation=0.3, perturbation=5", tuning.Describe());
        Assert.Equal(
            "temperature=2 (SA), mutation=0.3 (GA), perturbation=5 (ILS)",
            tuning.Describe(tagSolvers: true));
        Assert.Equal(string.Empty, SolverTuning.None.Describe());
        Assert.True(SolverTuning.None.IsEmpty);
    }
}
