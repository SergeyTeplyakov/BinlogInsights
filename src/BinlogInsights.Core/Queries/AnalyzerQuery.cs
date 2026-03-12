using System.Text.RegularExpressions;
using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogInsights.Core.Queries;

public static class AnalyzerQuery
{
    private static readonly Regex TotalAnalyzerTimeRegex = new(
        @"Total analyzer execution time:\s+[\d\.]+\s+seconds", RegexOptions.Compiled);
    private static readonly Regex TotalGeneratorTimeRegex = new(
        @"Total generator execution time:\s+[\d\.]+\s+seconds", RegexOptions.Compiled);

    /// <summary>
    /// Gets the most expensive Roslyn analyzers/generators across the entire build.
    /// </summary>
    public static IReadOnlyList<AggregatedAnalyzerData> GetExpensiveAnalyzers(
        Build build,
        int limit = 20,
        int offset = 0)
    {
        var analyzerStats = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);

        build.VisitAllChildren<MSBuildTask>(task =>
        {
            if (!string.Equals(task.Name, "Csc", StringComparison.OrdinalIgnoreCase))
                return;

            var data = ParseCscTask(task);
            if (data == null) return;

            AggregateAssemblies(data.AnalyzerAssemblies, analyzerStats);
            AggregateAssemblies(data.GeneratorAssemblies, analyzerStats);
        });

        return analyzerStats
            .Select(kvp =>
            {
                var durations = kvp.Value;
                return new AggregatedAnalyzerData(
                    kvp.Key,
                    durations.Count,
                    durations.Sum(),
                    durations.Count > 0 ? durations.Sum() / durations.Count : 0,
                    durations.Count > 0 ? durations.Min() : 0,
                    durations.Count > 0 ? durations.Max() : 0);
            })
            .OrderByDescending(a => a.TotalDurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Extracts analyzer/generator data from a specific Csc task.
    /// </summary>
    public static CscAnalyzerData? ParseCscTask(MSBuildTask task)
    {
        if (!string.Equals(task.Name, "Csc", StringComparison.OrdinalIgnoreCase))
            return null;

        var messages = task.Children.OfType<Message>()
            .Select(m => m.Text)
            .Where(t => t != null)
            .Cast<string>()
            .ToList();

        var analyzerAssemblies = new List<AssemblyAnalyzerData>();
        var generatorAssemblies = new List<AssemblyAnalyzerData>();

        List<AssemblyAnalyzerData>? currentSection = null;
        string? currentAssembly = null;
        var currentAnalyzers = new List<AnalyzerInfo>();

        foreach (var message in messages)
        {
            if (message.Contains("CompilerServer:"))
                continue;

            if (TotalAnalyzerTimeRegex.IsMatch(message))
            {
                currentSection = analyzerAssemblies;
                currentAssembly = null;
                continue;
            }

            if (TotalGeneratorTimeRegex.IsMatch(message))
            {
                SaveCurrentAssembly(currentSection, currentAssembly, currentAnalyzers);
                currentSection = generatorAssemblies;
                currentAssembly = null;
                currentAnalyzers = [];
                continue;
            }

            if (currentSection == null)
                continue;

            if (message.Contains(", Version="))
            {
                SaveCurrentAssembly(currentSection, currentAssembly, currentAnalyzers);
                var parsed = ParseLine(message);
                currentAssembly = parsed.name;
                currentAnalyzers = [];
                continue;
            }

            if (currentAssembly != null)
            {
                var parsed = ParseLine(message);
                if (parsed.durationMs > 0 || !string.IsNullOrWhiteSpace(parsed.name))
                {
                    currentAnalyzers.Add(new AnalyzerInfo(parsed.name, parsed.durationMs));
                }
            }
        }

        SaveCurrentAssembly(currentSection, currentAssembly, currentAnalyzers);

        if (analyzerAssemblies.Count == 0 && generatorAssemblies.Count == 0)
            return null;

        return new CscAnalyzerData(analyzerAssemblies, generatorAssemblies);
    }

    private static void SaveCurrentAssembly(
        List<AssemblyAnalyzerData>? section,
        string? assemblyName,
        List<AnalyzerInfo> analyzers)
    {
        if (section == null || assemblyName == null || analyzers.Count == 0)
            return;

        var totalMs = analyzers.Sum(a => a.DurationMs);
        section.Add(new AssemblyAnalyzerData(assemblyName, totalMs, analyzers.ToList()));
    }

    private static void AggregateAssemblies(
        IReadOnlyList<AssemblyAnalyzerData> assemblies,
        Dictionary<string, List<long>> stats)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var analyzer in assembly.Analyzers)
            {
                if (!stats.TryGetValue(analyzer.Name, out var durations))
                {
                    durations = [];
                    stats[analyzer.Name] = durations;
                }
                durations.Add(analyzer.DurationMs);
            }
        }
    }

    private static (string name, long durationMs) ParseLine(string line)
    {
        // Lines are formatted: "  <seconds>   <percentage>   <name>"
        var columns = line.Split(["  "], StringSplitOptions.RemoveEmptyEntries);

        if (columns.Length >= 3)
        {
            if (double.TryParse(columns[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            {
                var name = columns[^1].Trim();
                return (name, (long)(seconds * 1000));
            }
        }

        return (line.Trim(), 0);
    }
}
