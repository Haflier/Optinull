using System.Globalization;

namespace Optinull.Application.Problems;

public static class ProblemParameterReader
{
    /// <summary>Checks names, integer format and ranges; fills in defaults.</summary>
    public static bool TryRead(
        IReadOnlyList<ProblemParameter> specs,
        IReadOnlyDictionary<string, string> values,
        out Dictionary<string, int> result,
        out string? error)
    {
        result = specs.ToDictionary(spec => spec.Name, spec => spec.Default, StringComparer.OrdinalIgnoreCase);
        error = null;

        foreach (var (key, text) in values)
        {
            var spec = specs.FirstOrDefault(s =>
                string.Equals(s.Name, key, StringComparison.OrdinalIgnoreCase));

            if (spec is null)
            {
                var allowed = specs.Count == 0
                    ? "none"
                    : string.Join(", ", specs.Select(s => s.Name));

                error = $"Unknown parameter '{key}'. Allowed here: {allowed}.";
                return false;
            }

            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
                number < spec.Min || number > spec.Max)
            {
                error = $"{spec.Name} must be a whole number from {spec.Min} to {spec.Max}.";
                return false;
            }

            result[spec.Name] = number;
        }

        return true;
    }

    public static string Describe(IReadOnlyList<ProblemParameter> specs) =>
        string.Join(
            ", ",
            specs.Select(s => $"{s.Name}={s.Min}-{s.Max} (default {s.Default})"));
}
