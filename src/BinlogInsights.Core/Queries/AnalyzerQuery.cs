using System.Globalization;
using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogInsights.Core.Queries;

public static class AnalyzerQuery
{
    /// <summary>
    /// Gets the most expensive Roslyn analyzers/generators across the entire build.
    /// After BuildAnalyzer.AnalyzeBuild(), Csc tasks contain "Analyzer Report" and
    /// "Generator Report" Folder nodes with structured TimedMessage children.
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
    /// Extracts analyzer/generator data from a Csc task by reading the structured
    /// "Analyzer Report" and "Generator Report" Folder nodes that BuildAnalyzer creates.
    /// </summary>
    public static CscAnalyzerData? ParseCscTask(MSBuildTask task)
    {
        if (!string.Equals(task.Name, "Csc", StringComparison.OrdinalIgnoreCase))
            return null;

        var analyzerAssemblies = new List<AssemblyAnalyzerData>();
        var generatorAssemblies = new List<AssemblyAnalyzerData>();

        foreach (var folder in task.Children.OfType<Folder>())
        {
            var target = folder.Name switch
            {
                "Analyzer Report" => analyzerAssemblies,
                "Generator Report" => generatorAssemblies,
                _ => null
            };

            if (target == null)
                continue;

            // Each assembly is a Folder child; skip TimedMessage headers (total time, column header).
            foreach (var assemblyFolder in folder.Children.OfType<Folder>())
            {
                var parsed = ParseTimedLine(assemblyFolder.Name);
                if (parsed.name == null)
                    continue;

                var analyzers = new List<AnalyzerInfo>();
                foreach (var entry in assemblyFolder.Children.OfType<TimedMessage>())
                {
                    var entryParsed = ParseTimedLine(entry.Text);
                    if (entryParsed.name != null)
                        analyzers.Add(new AnalyzerInfo(entryParsed.name, entryParsed.durationMs));
                }

                var totalMs = analyzers.Count > 0 ? analyzers.Sum(a => a.DurationMs) : parsed.durationMs;
                target.Add(new AssemblyAnalyzerData(parsed.name, totalMs, analyzers));
            }
        }

        if (analyzerAssemblies.Count == 0 && generatorAssemblies.Count == 0)
            return null;

        return new CscAnalyzerData(analyzerAssemblies, generatorAssemblies);
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

    /// <summary>
    /// Parses a timed line in the format: "0.012   59   Some.Analyzer.Name (CA1234)"
    /// or "&lt;0.001   &lt;1   Some.Analyzer.Name".
    /// </summary>
    private static (string? name, long durationMs) ParseTimedLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return (null, 0);

        // Split on runs of whitespace. Format: "<seconds>   <%>   <name...>"
        var parts = line.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return (null, 0);

        var secondsStr = parts[0].TrimStart('<');
        if (!double.TryParse(secondsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return (null, 0);

        // Name is everything after the first two columns (seconds + percentage).
        var name = string.Join(" ", parts.Skip(2));
        return (name, (long)(seconds * 1000));
    }
}
