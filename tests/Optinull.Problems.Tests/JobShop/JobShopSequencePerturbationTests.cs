using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopSequencePerturbationTests
{
    [Fact]
    public void Perturb_PreservesJobMultiset()
    {
        var sequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var result =
            new JobShopSequencePerturbation()
                .Perturb(
                    sequence,
                    swapCount: 3,
                    new Random(42));

        Assert.Equal(
            sequence.JobIds.OrderBy(id => id),
            result.JobIds.OrderBy(id => id));
    }

    [Fact]
    public void Perturb_ChangesSequence()
    {
        var sequence =
            new JobSequence(
            [
                0, 0, 0,
                1, 1, 1,
                2, 2, 2
            ]);

        var result =
            new JobShopSequencePerturbation()
                .Perturb(
                    sequence,
                    swapCount: 3,
                    new Random(42));

        Assert.NotEqual(
            sequence.JobIds,
            result.JobIds);
    }

    [Fact]
    public void Perturb_RejectsInvalidSwapCount()
    {
        var sequence =
            new JobSequence(
            [
                0, 1, 0, 1
            ]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new JobShopSequencePerturbation()
                    .Perturb(
                        sequence,
                        0,
                        new Random(42)));
    }
}
