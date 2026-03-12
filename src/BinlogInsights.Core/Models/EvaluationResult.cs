namespace BinlogInsights.Core.Models;

/// <summary>
/// Data about a project evaluation from the binlog.
/// </summary>
public record EvaluationData(
    int Id,
    string ProjectFile,
    long DurationMs);
