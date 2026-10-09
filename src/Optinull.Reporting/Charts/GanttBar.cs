namespace Optinull.Reporting.Charts;

/// <summary>One bar of a Gantt chart: a task on a row between two times.</summary>
public sealed record GanttBar(
    int Row,
    double Start,
    double End,
    int ColorIndex,
    string Label);
