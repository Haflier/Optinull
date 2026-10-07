using Optinull.Domain.Constraints;
using Optinull.Domain.Expressions;
using Optinull.Domain.Objectives;
using Optinull.Domain.Variables;

namespace Optinull.Domain.Problems;

public sealed class OptimizationProblem
{
    private readonly List<Variable> _variables = [];
    private readonly List<Constraint> _constraints = [];

    public string Name { get; }

    public IReadOnlyList<Variable> Variables => _variables;

    public IReadOnlyList<Constraint> Constraints => _constraints;

    public Objective? Objective { get; private set; }

    public OptimizationProblem(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Problem name cannot be empty.",
                nameof(name));
        }

        Name = name;
    }

    public Variable AddBinaryVariable(string name)
    {
        return AddVariable(
            name,
            VariableType.Binary,
            0,
            1);
    }

    public Variable AddIntegerVariable(
        string name,
        int lowerBound,
        int upperBound)
    {
        return AddVariable(
            name,
            VariableType.Integer,
            lowerBound,
            upperBound);
    }

    public Variable AddContinuousVariable(
        string name,
        double lowerBound,
        double upperBound)
    {
        return AddVariable(
            name,
            VariableType.Continuous,
            lowerBound,
            upperBound);
    }

    public void AddConstraint(Constraint constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);

        _constraints.Add(constraint);
    }

    public void Maximize(Expression expression)
    {
        SetObjective(
            expression,
            ObjectiveType.Maximize);
    }

    public void Minimize(Expression expression)
    {
        SetObjective(
            expression,
            ObjectiveType.Minimize);
    }

    private void SetObjective(
        Expression expression,
        ObjectiveType type)
    {
        ArgumentNullException.ThrowIfNull(expression);

        if (Objective is not null)
        {
            throw new InvalidOperationException(
                "An optimization problem can only have one objective.");
        }

        Objective = new Objective(
            expression,
            type);
    }

    private Variable AddVariable(
        string name,
        VariableType type,
        double lowerBound,
        double upperBound)
    {
        if (_variables.Any(v => v.Name == name))
        {
            throw new ArgumentException(
                $"A variable named '{name}' already exists.");
        }

        var variable = new Variable(
            name,
            type,
            lowerBound,
            upperBound);

        _variables.Add(variable);

        return variable;
    }
}
