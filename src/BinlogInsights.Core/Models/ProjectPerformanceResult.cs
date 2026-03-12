namespace BinlogInsights.Core.Models;

/// <summary>
/// Build time data for a single project, aggregated from its targets.
/// </summary>
public record ProjectPerformanceData(
    string ProjectFile,
    int ProjectId,
    long ExclusiveDurationMs,
    long InclusiveDurationMs,
    int TargetCount);

/// <summary>
/// Target execution summary within a project.
/// </summary>
public record ProjectTargetData(
    int Id,
    string Name,
    long InclusiveDurationMs,
    long ExclusiveDurationMs,
    bool Skipped);
