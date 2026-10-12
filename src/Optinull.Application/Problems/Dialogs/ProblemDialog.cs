using System.Globalization;

namespace Optinull.Application.Problems;

/// <summary>What the bot says next in a guided input dialog.</summary>
public abstract record DialogResult;

/// <summary>Ask something, or ask again after an answer that could not be used.</summary>
public sealed record DialogAsk(string Text) : DialogResult;

/// <summary>Everything was collected and the problem is ready. Notes recap what was entered.</summary>
public sealed record DialogDone(IProblemInstance Instance, string? Notes) : DialogResult;

/// <summary>The collected data could not be used after all; the dialog is over.</summary>
public sealed record DialogFailed(string Message) : DialogResult;

/// <summary>
/// Collects a problem from a user one question at a time. A dialog belongs to one chat
/// and is created by the problem module.
/// </summary>
public interface IProblemDialog
{
    /// <summary>The first question.</summary>
    string Start();

    /// <summary>Takes the user's next message.</summary>
    DialogResult Answer(string text);
}

/// <summary>A question answered by one whole number.</summary>
public sealed record DialogQuestion(string Key, string Prompt, int Min, int Max);

/// <summary>What became of one line of data.</summary>
public readonly record struct LineResult(string? Normalized, string? Echo, string? Error);

/// <summary>
/// The data lines that follow the questions: as many as one of the answers says.
/// Lines may arrive one per message or many in one.
/// </summary>
public sealed record DialogEntries(
    string CountKey,
    Func<IReadOnlyDictionary<string, int>, int, int, string> Prompt,
    Func<string, IReadOnlyDictionary<string, int>, IReadOnlyList<string>, LineResult> ParseLine,
    Func<IReadOnlyDictionary<string, int>, IReadOnlyList<string>, string> BuildText);

/// <summary>
/// The shared engine: some whole-number questions, then the data lines. The collected
/// text goes through the module's own parser, so a guided instance is exactly what the
/// text format would have produced. "undo" removes the last line.
/// </summary>
internal sealed class GuidedDialog : IProblemDialog
{
    private const int MaxEchoes = 6;

    private readonly object _gate = new();
    private readonly IProblemModule _module;
    private readonly IReadOnlyList<DialogQuestion> _questions;
    private readonly DialogEntries _entries;
    private readonly Dictionary<string, int> _answers = [];
    private readonly List<string> _lines = [];

    private int _next;

    public GuidedDialog(
        IProblemModule module,
        IReadOnlyList<DialogQuestion> questions,
        DialogEntries entries)
    {
        _module = module;
        _questions = questions;
        _entries = entries;
    }

    public string Start() => _questions[0].Prompt;

    public DialogResult Answer(string text)
    {
        lock (_gate)
        {
            var input = (text ?? string.Empty).Trim();

            return _next < _questions.Count
                ? AnswerQuestion(input)
                : AnswerEntries(input);
        }
    }

    private DialogResult AnswerQuestion(string input)
    {
        var question = _questions[_next];

        if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
            number < question.Min || number > question.Max)
        {
            return new DialogAsk(
                $"That should be a whole number from {question.Min} to {question.Max}.\n\n{question.Prompt}");
        }

        _answers[question.Key] = number;
        _next++;

        return new DialogAsk(_next < _questions.Count ? _questions[_next].Prompt : EntryPrompt());
    }

    private DialogResult AnswerEntries(string input)
    {
        var total = _answers[_entries.CountKey];

        if (input.Equals("undo", StringComparison.OrdinalIgnoreCase))
        {
            if (_lines.Count == 0)
                return new DialogAsk($"There is nothing to undo yet.\n\n{EntryPrompt()}");

            _lines.RemoveAt(_lines.Count - 1);

            return new DialogAsk($"Removed the last one.\n\n{EntryPrompt()}");
        }

        var echoes = new List<string>();
        var accepted = 0;
        string? problem = null;

        var lines = input
            .Replace("\r", string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));

        foreach (var line in lines)
        {
            if (_lines.Count >= total)
            {
                echoes.Add($"Ignored the extra lines: you already gave all {total}.");
                break;
            }

            var result = _entries.ParseLine(line, _answers, _lines);

            if (result.Error is not null)
            {
                var shown = line.Length > 40 ? line[..40] + "..." : line;

                problem = $"\"{shown}\": {result.Error}";
                break;
            }

            _lines.Add(result.Normalized!);
            accepted++;

            if (result.Echo is not null)
                echoes.Add(result.Echo);
        }

        // A long paste would make a message too long to send, so recap it briefly.
        if (echoes.Count > MaxEchoes)
            echoes = [$"Accepted {accepted} lines."];

        if (problem is not null)
            echoes.Add($"That line was not used - {problem}");

        var notes = echoes.Count == 0 ? null : string.Join("\n", echoes);

        if (_lines.Count < total)
        {
            return new DialogAsk(
                notes is null ? EntryPrompt() : $"{notes}\n\n{EntryPrompt()}");
        }

        if (!_module.TryParseCustom(_entries.BuildText(_answers, _lines), out var instance, out var error))
            return new DialogFailed($"I could not use that data: {error}");

        return new DialogDone(instance, notes);
    }

    private string EntryPrompt() =>
        _entries.Prompt(_answers, _lines.Count, _answers[_entries.CountKey]);
}

/// <summary>Helpers shared by the problems' line parsers.</summary>
internal static class DialogNumbers
{
    private static readonly char[] Separators = [' ', '\t', ':'];

    public static string[] Tokens(string text, params char[] extraSeparators) =>
        text.Split(Separators.Concat(extraSeparators).ToArray(), StringSplitOptions.RemoveEmptyEntries);

    public static bool TryWhole(string token, out int value) =>
        int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
