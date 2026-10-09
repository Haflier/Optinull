using System.Globalization;

namespace Optinull.Problems.JobShop.Parsing;

/// <summary>
/// Reads an instance from text: a "jobs machines" header, then one line per
/// job with "machine time" pairs. Blank lines and lines starting with # are ignored.
/// </summary>
public static class JobShopTextParser
{
    private static readonly char[] Separators = [' ', '\t', ','];

    public static bool TryParse(
        string? text,
        JobShopInputLimits limits,
        out JobShopProblem? problem,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(limits);

        error = Parse(text, limits, out problem);

        return error is null;
    }

    // Returns an error message, or null on success.
    private static string? Parse(
        string? text,
        JobShopInputLimits limits,
        out JobShopProblem? problem)
    {
        problem = null;

        if (string.IsNullOrWhiteSpace(text))
            return "The instance is empty.";

        if (text.Length > limits.MaxCharacters)
            return $"The instance is too long (limit {limits.MaxCharacters} characters).";

        var lines = text
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select((content, index) => (Number: index + 1, Content: content.Trim()))
            .Where(line => line.Content.Length > 0 && !line.Content.StartsWith('#'))
            .ToList();

        if (lines.Count == 0)
            return "The instance is empty.";

        var headerLine = lines[0];

        if (!TryReadNumbers(headerLine.Content, out var header) || header.Length != 2)
        {
            return $"Line {headerLine.Number}: the first line must be " +
                   "'<jobs> <machines>', for example '3 3'.";
        }

        var jobCount = header[0];
        var machineCount = header[1];

        if (jobCount <= 0 || machineCount <= 0)
        {
            return $"Line {headerLine.Number}: the number of jobs and machines must be positive.";
        }

        if (jobCount > limits.MaxJobs)
            return $"Too many jobs ({jobCount}); the limit is {limits.MaxJobs}.";

        if (machineCount > limits.MaxMachines)
            return $"Too many machines ({machineCount}); the limit is {limits.MaxMachines}.";

        if (lines.Count - 1 != jobCount)
        {
            return $"Expected {jobCount} job lines after the header, found {lines.Count - 1}.";
        }

        var jobs = new List<Job>(jobCount);
        var operationCount = 0;

        for (var jobId = 0; jobId < jobCount; jobId++)
        {
            var line = lines[jobId + 1];

            if (!TryReadNumbers(line.Content, out var numbers))
                return $"Line {line.Number}: only whole numbers are allowed.";

            if (numbers.Length == 0 || numbers.Length % 2 != 0)
            {
                return $"Line {line.Number}: expected 'machine time' pairs, " +
                       $"but found {numbers.Length} numbers.";
            }

            var operations = new List<JobOperation>(numbers.Length / 2);

            for (var i = 0; i < numbers.Length; i += 2)
            {
                var machine = numbers[i];
                var time = numbers[i + 1];

                if (machine < 0 || machine >= machineCount)
                {
                    return $"Line {line.Number}: machine {machine} does not exist " +
                           $"(machines are numbered 0 to {machineCount - 1}).";
                }

                if (time <= 0 || time > limits.MaxProcessingTime)
                {
                    return $"Line {line.Number}: processing time must be between " +
                           $"1 and {limits.MaxProcessingTime}.";
                }

                operations.Add(new JobOperation(machine, time));
            }

            operationCount += operations.Count;

            if (operationCount > limits.MaxOperations)
            {
                return $"Too many operations; the limit is {limits.MaxOperations}.";
            }

            jobs.Add(new Job(jobId, operations));
        }

        try
        {
            problem = new JobShopProblem(machineCount, jobs);
            return null;
        }
        catch (ArgumentException exception)
        {
            return exception.Message;
        }
    }

    private static bool TryReadNumbers(string line, out int[] numbers)
    {
        var tokens = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        var result = new int[tokens.Length];

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(
                    tokens[i],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out result[i]))
            {
                numbers = [];
                return false;
            }
        }

        numbers = result;
        return true;
    }
}
