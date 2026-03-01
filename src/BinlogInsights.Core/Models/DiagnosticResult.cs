namespace BinlogInsights.Core.Models;

public record DiagnosticResult(
    string Severity, // "error" or "warning"
    string? Code,
    string Message,
    string? File,
    int? Line,
    int? Column,
    string? ProjectFile,
    string? TargetName,
    string? TaskName);
