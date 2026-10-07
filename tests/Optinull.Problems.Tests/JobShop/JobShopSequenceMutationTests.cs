using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public sealed class JobShopSequenceMutationTests
{
    [Fact]
    public void Mutate_PreservesSequenceLength()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var mutation =
            new JobShopSequenceMutation();

        var result =
            mutation.Mutate(
                sequence,
                new Random(42));

        Assert.Equal(
            sequence.Count,
            result.Count);
    }

    [Fact]
    public void Mutate_PreservesJobCounts()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var mutation =
            new JobShopSequenceMutation();

        var result =
            mutation.Mutate(
                sequence,
                new Random(42));

        Assert.Equal(
            sequence.JobIds.Count(id => id == 0),
            result.JobIds.Count(id => id == 0));

        Assert.Equal(
            sequence.JobIds.Count(id => id == 1),
            result.JobIds.Count(id => id == 1));
    }

    [Fact]
    public void Mutate_ProducesValidJobSequence()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var mutation =
            new JobShopSequenceMutation();

        var result =
            mutation.Mutate(
                sequence,
                new Random(42));

        Assert.All(
            result.JobIds,
            jobId => Assert.True(
                jobId == 0 ||
                jobId == 1));
    }

    [Fact]
    public void Mutate_DoesNotModifyOriginalSequence()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var original =
            sequence.JobIds.ToArray();

        var mutation =
            new JobShopSequenceMutation();

        mutation.Mutate(
            sequence,
            new Random(42));

        Assert.Equal(
            original,
            sequence.JobIds);
    }

    [Fact]
    public void Mutate_IsReproducibleWithSameSeed()
    {
        var sequence =
            new JobSequence([0, 1, 0, 1]);

        var mutation =
            new JobShopSequenceMutation();

        var first =
            mutation.Mutate(
                sequence,
                new Random(42));

        var second =
            mutation.Mutate(
                sequence,
                new Random(42));

        Assert.Equal(
            first.JobIds,
            second.JobIds);
    }
}
