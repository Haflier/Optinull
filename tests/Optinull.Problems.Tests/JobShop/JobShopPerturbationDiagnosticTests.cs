using Optinull.Problems.JobShop;
using Optinull.Problems.JobShop.Benchmarks;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopPerturbationDiagnosticTests
{
    [Fact]
    public void InspectPerturbationDistance()
    {
        var problem =
            JobShopBenchmarkInstances.Ft06();

        var sequence =
            new JobSequence(
            [
                2, 0, 1, 2, 1, 1, 2, 5, 5, 3, 3, 5,
                4, 0, 2, 1, 3, 3, 5, 4, 5, 4, 1, 0,
                5, 4, 0, 1, 2, 3, 3, 0, 2, 4, 0, 4
            ]);

        var perturbation =
            new JobShopSequencePerturbation();

        var evaluator =
            new JobShopEvaluator();

        var random =
            new Random(42);

        var originalMakespan =
            evaluator
                .Evaluate(problem, sequence)
                .ObjectiveValue;

        Console.WriteLine(
            $"Original makespan: {originalMakespan}");

        for (var trial = 1; trial <= 20; trial++)
        {
            var perturbed =
                perturbation.Perturb(
                    sequence,
                    swapCount: 3,
                    random);

            var changedPositions =
                sequence.JobIds
                    .Zip(
                        perturbed.JobIds,
                        (original, changed) =>
                            original != changed)
                    .Count(changed => changed);

            var makespan =
                evaluator
                    .Evaluate(problem, perturbed)
                    .ObjectiveValue;

            Console.WriteLine(
                $"Trial {trial}: " +
                $"changed positions = {changedPositions}, " +
                $"makespan = {makespan}, " +
                $"sequence = [{string.Join(", ", perturbed.JobIds)}]");
        }
    }
}
