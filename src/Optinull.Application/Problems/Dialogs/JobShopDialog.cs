using Optinull.Problems.JobShop.Parsing;

namespace Optinull.Application.Problems;

/// <summary>Asks for machines, then jobs, then each job's steps; machines are numbered from 1.</summary>
internal static class JobShopDialog
{
    public static IProblemDialog Create(IProblemModule module)
    {
        var limits = JobShopInputLimits.Default;

        return new GuidedDialog(
            module,
            [
                new("machines",
                    "Let's describe your job shop.\n" +
                    $"How many machines does it have? (1-{limits.MaxMachines})",
                    1, limits.MaxMachines),
                new("jobs",
                    $"How many jobs need to be scheduled? (1-{limits.MaxJobs})",
                    1, limits.MaxJobs)
            ],
            new DialogEntries(
                "jobs",
                Prompt,
                (line, answers, lines) => ParseJob(line, answers["machines"], lines, limits),
                (answers, lines) => $"{answers["jobs"]} {answers["machines"]}\n{string.Join("\n", lines)}"));
    }

    private static string Prompt(IReadOnlyDictionary<string, int> answers, int done, int total)
    {
        var machines = answers["machines"];

        if (done == 0)
        {
            return
                $"Job 1 of {total}. Send its steps in the order they must run, as \"machine time\" pairs.\n" +
                $"Machines are numbered 1-{machines}. Example:\n1 4, 2 3, 3 5\n" +
                "(machine 1 for 4 time units, then machine 2 for 3, then machine 3 for 5)\n\n" +
                "You can send several jobs at once, one per line. Send \"undo\" to remove the last job.";
        }

        return
            $"Job {done + 1} of {total}: steps as \"machine time\" pairs, for example 1 4, 2 3 " +
            $"(machines 1-{machines}). Several jobs at once: one per line. \"undo\" removes the last job.";
    }

    // "1 4, 2 3, 3 5" or "1 4 2 3 3 5" or "1:4 2:3".
    private static LineResult ParseJob(
        string line,
        int machines,
        IReadOnlyList<string> lines,
        JobShopInputLimits limits)
    {
        var pairs = new List<(int Machine, int Time)>();

        if (line.Contains(',') || line.Contains(';'))
        {
            foreach (var part in line.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var tokens = DialogNumbers.Tokens(part);

                if (tokens.Length != 2 ||
                    !DialogNumbers.TryWhole(tokens[0], out var machine) ||
                    !DialogNumbers.TryWhole(tokens[1], out var time))
                {
                    return Fail($"\"{part.Trim()}\" should be two whole numbers: a machine and a time.");
                }

                pairs.Add((machine, time));
            }
        }
        else
        {
            var tokens = DialogNumbers.Tokens(line);
            var numbers = new List<int>();

            foreach (var token in tokens)
            {
                if (!DialogNumbers.TryWhole(token, out var number))
                    return Fail("only whole numbers are allowed.");

                numbers.Add(number);
            }

            if (numbers.Count == 0 || numbers.Count % 2 != 0)
            {
                return Fail(
                    $"I need \"machine time\" pairs, but found {numbers.Count} numbers.");
            }

            for (var i = 0; i < numbers.Count; i += 2)
                pairs.Add((numbers[i], numbers[i + 1]));
        }

        if (pairs.Count == 0)
            return Fail("it has no steps.");

        foreach (var (machine, time) in pairs)
        {
            if (machine < 1 || machine > machines)
                return Fail($"machine {machine} does not exist (machines are 1-{machines}).");

            if (time < 1 || time > limits.MaxProcessingTime)
                return Fail($"a time must be between 1 and {limits.MaxProcessingTime}.");
        }

        var steps = lines.Sum(existing => DialogNumbers.Tokens(existing).Length / 2) + pairs.Count;

        if (steps > limits.MaxOperations)
            return Fail($"that is too many steps in total (the limit is {limits.MaxOperations}).");

        // The text format counts machines from 0.
        var normalized = string.Join(" ", pairs.Select(pair => $"{pair.Machine - 1} {pair.Time}"));
        var echo = $"Job {lines.Count + 1}: " +
                   string.Join(" -> ", pairs.Select(pair => $"M{pair.Machine} ({pair.Time})"));

        return new LineResult(normalized, echo, null);
    }

    private static LineResult Fail(string error) => new(null, null, error);
}
