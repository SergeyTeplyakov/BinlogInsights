namespace BinlogInsights.Core.Models;

public record ImportResult(
    string ImportedFile,
    string? ImportedBy,
    int? Line,
    int? Column,
    bool IsMissing,
    IReadOnlyList<ImportResult> Children);
