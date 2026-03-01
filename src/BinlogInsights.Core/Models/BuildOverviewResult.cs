namespace BinlogInsights.Core.Models;

public record BuildOverviewResult(
    bool Succeeded,
    string? MsBuildVersion,
    TimeSpan Duration,
    int ProjectCount,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ProjectSummary> Projects);

public record ProjectSummary(
    string ProjectFile,
    string? TargetFramework,
    bool Succeeded,
    TimeSpan Duration);
