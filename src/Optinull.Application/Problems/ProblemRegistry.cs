using System.Diagnostics.CodeAnalysis;

namespace Optinull.Application.Problems;

public sealed class ProblemRegistry
{
    private readonly List<IProblemModule> _modules;

    public ProblemRegistry(IEnumerable<IProblemModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);

        _modules = modules.ToList();

        if (_modules.Count == 0)
            throw new ArgumentException("At least one problem module is required.", nameof(modules));

        var names = _modules
            .Select(module => module.Id)
            .Concat(_modules.SelectMany(module => module.BuiltInNames))
            .Select(name => name.ToLowerInvariant())
            .ToList();

        var duplicate = names
            .GroupBy(name => name)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"The name '{duplicate.Key}' is used by more than one module or instance.");
        }
    }

    public static ProblemRegistry CreateDefault() =>
        new([new JobShopModule(), new TspModule()]);

    public IReadOnlyList<IProblemModule> Modules => _modules;

    public IReadOnlyList<string> BuiltInNames =>
        _modules.SelectMany(module => module.BuiltInNames).ToList();

    public IProblemModule? FindModule(string id) =>
        _modules.FirstOrDefault(module =>
            string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase));

    public bool TryGetBuiltIn(
        string name,
        [NotNullWhen(true)] out IProblemInstance? instance)
    {
        foreach (var module in _modules)
        {
            if (module.TryGetBuiltIn(name, out instance))
                return true;
        }

        instance = null;
        return false;
    }
}
