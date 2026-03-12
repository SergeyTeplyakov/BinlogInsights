namespace BinlogInsights.Core.Models;

/// <summary>
/// Aggregated target execution data across the entire build, grouped by target name.
/// </summary>
public record TargetExecutionData(
    string TargetName,
    int ExecutionCount,
    long TotalExclusiveMs,
    long TotalInclusiveMs,
    long MinDurationMs,
    long MaxDurationMs);

/// <summary>
/// Detailed info about a specific target execution within a project.
/// </summary>
public record TargetInfo(
    int Id,
    string Name,
    string ProjectFile,
    int ProjectId,
    long DurationMs,
    bool Skipped,
    string? DependsOnTargets,
    string? ParentTarget);
