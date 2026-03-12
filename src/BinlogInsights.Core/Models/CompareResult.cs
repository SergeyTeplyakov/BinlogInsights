namespace BinlogInsights.Core.Models;

public record CompareResult(
    BuildOverviewResult OverviewA,
    BuildOverviewResult OverviewB,
    IReadOnlyList<PropertyDiff> PropertyDiffs,
    IReadOnlyList<string> ImportsOnlyInA,
    IReadOnlyList<string> ImportsOnlyInB,
    IReadOnlyList<string> ReferencesOnlyInA,
    IReadOnlyList<string> ReferencesOnlyInB,
    IReadOnlyList<PackageDiff> PackageDiffs,
    IReadOnlyList<PackageDiff> SolutionPackageDiffs);

public record PropertyDiff(
    string Name,
    string? ValueA,
    string? ValueB);

public record PackageDiff(
    string PackageName,
    string? VersionA,
    string? VersionB);
