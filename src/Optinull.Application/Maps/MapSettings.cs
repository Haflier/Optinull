using Optinull.Application.Optimization;

namespace Optinull.Application.Maps;

/// <summary>The algorithm choices that came with the command: what to run once the places are collected.</summary>
internal sealed record MapSettings(
    SolverKind Solver,
    int? Iterations,
    int Seed,
    SolverTuning? Tuning);
