namespace BinlogInsights.Core.Models;

/// <summary>
/// A single Roslyn analyzer or source generator with its execution duration.
/// </summary>
public record AnalyzerInfo(
    string Name,
    long DurationMs);

/// <summary>
/// Analyzer data for an assembly, containing individual analyzer entries.
/// </summary>
public record AssemblyAnalyzerData(
    string AssemblyName,
    long TotalDurationMs,
    IReadOnlyList<AnalyzerInfo> Analyzers);

/// <summary>
/// Analyzer and generator data extracted from a single Csc task invocation.
/// </summary>
public record CscAnalyzerData(
    IReadOnlyList<AssemblyAnalyzerData> AnalyzerAssemblies,
    IReadOnlyList<AssemblyAnalyzerData> GeneratorAssemblies);

/// <summary>
/// Aggregated analyzer execution data across the entire build.
/// </summary>
public record AggregatedAnalyzerData(
    string Name,
    int ExecutionCount,
    long TotalDurationMs,
    long AvgDurationMs,
    long MinDurationMs,
    long MaxDurationMs);
