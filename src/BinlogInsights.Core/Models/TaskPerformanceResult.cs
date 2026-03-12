namespace BinlogInsights.Core.Models;

/// <summary>
/// Aggregated task execution data across the entire build, grouped by task name.
/// </summary>
public record TaskExecutionData(
    string TaskName,
    int ExecutionCount,
    long TotalDurationMs,
    long MinDurationMs,
    long MaxDurationMs,
    long AvgDurationMs);

/// <summary>
/// Basic task info within a target.
/// </summary>
public record TaskInfo(
    int Id,
    string Name,
    string ProjectFile,
    string TargetName,
    long DurationMs);

/// <summary>
/// Detailed task info including parameters and output messages.
/// </summary>
public record TaskDetails(
    int Id,
    string Name,
    string ProjectFile,
    string TargetName,
    long DurationMs,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<string> OutputMessages);
