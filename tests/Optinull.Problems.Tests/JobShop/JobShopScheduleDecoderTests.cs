using Optinull.Problems.JobShop;

namespace Optinull.Problems.Tests.JobShop;

public class JobShopScheduleDecoderTests
{
    [Fact]
    public void Decode_CreatesValidSchedule()
    {
        var problem = CreateProblem();

        var decoder = new JobShopScheduleDecoder();

        var schedule = decoder.Decode(
            problem,
            new JobSequence([0, 1, 0, 1]));

        Assert.Equal(4, schedule.Operations.Count);
    }

    [Fact]
    public void Decode_CalculatesExpectedStartAndEndTimes()
    {
        var problem = CreateProblem();

        var decoder = new JobShopScheduleDecoder();

        var schedule = decoder.Decode(
            problem,
            new JobSequence([0, 1, 0, 1]));

        var operations = schedule.Operations;

        // J0-O0: M0 for 3
        Assert.Equal(0, operations[0].StartTime);
        Assert.Equal(3, operations[0].EndTime);

        // J1-O0: M1 for 4
        Assert.Equal(0, operations[1].StartTime);
        Assert.Equal(4, operations[1].EndTime);

        // J0-O1: M1 for 2.
        //
        // J0 is ready at 3.
        // M1 is ready at 4.
        // Therefore it starts at 4.
        Assert.Equal(4, operations[2].StartTime);
        Assert.Equal(6, operations[2].EndTime);

        // J1-O1: M0 for 1.
        //
        // J1 is ready at 4.
        // M0 is ready at 3.
        // Therefore it starts at 4.
        Assert.Equal(4, operations[3].StartTime);
        Assert.Equal(5, operations[3].EndTime);
    }

    [Fact]
    public void Decode_CalculatesMakespan()
    {
        var problem = CreateProblem();

        var decoder = new JobShopScheduleDecoder();

        var schedule = decoder.Decode(
            problem,
            new JobSequence([0, 1, 0, 1]));

        Assert.Equal(6, schedule.Makespan);
    }

    [Fact]
    public void Decode_RejectsWrongSequenceLength()
    {
        var problem = CreateProblem();

        var decoder = new JobShopScheduleDecoder();

        Assert.Throws<ArgumentException>(
            () => decoder.Decode(
                problem,
                new JobSequence([0, 1])));
    }

    [Fact]
    public void Decode_RejectsUnknownJob()
    {
        var problem = CreateProblem();

        var decoder = new JobShopScheduleDecoder();

        Assert.Throws<ArgumentException>(
            () => decoder.Decode(
                problem,
                new JobSequence([0, 1, 0, 99])));
    }

    [Fact]
    public void Decode_RejectsIncorrectJobOccurrences()
    {
        var problem = CreateProblem();

        var decoder = new JobShopScheduleDecoder();

        Assert.Throws<ArgumentException>(
            () => decoder.Decode(
                problem,
                new JobSequence([0, 0, 0, 1])));
    }

    private static JobShopProblem CreateProblem()
    {
        var job0 = new Job(
            0,
            [
                // J0: M0 → M1
                new JobOperation(0, 3),
                new JobOperation(1, 2)
            ]);

        var job1 = new Job(
            1,
            [
                // J1: M1 → M0
                new JobOperation(1, 4),
                new JobOperation(0, 1)
            ]);

        return new JobShopProblem(
            2,
            [job0, job1]);
    }
}
