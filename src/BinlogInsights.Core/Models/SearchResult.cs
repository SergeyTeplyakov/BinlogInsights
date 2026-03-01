namespace BinlogInsights.Core.Models;

public record SearchResult(
    string Message,
    string? ProjectFile,
    string? TargetName,
    string? TaskName,
    string NodeType);
