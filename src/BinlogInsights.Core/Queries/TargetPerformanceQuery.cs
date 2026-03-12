using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogInsights.Core.Queries;

public static class TargetPerformanceQuery
{
    // Targets that represent cross-project MSBuild calls (their duration should be subtracted for exclusive time)
    private static readonly HashSet<string> ProjectReferenceProtocolTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetTargetFrameworks", "GetTargetFrameworksWithPlatformForSingleTargetFramework",
        "GetTargetPath", "GetTargetPathWithTargetPlatformMoniker",
        "GetNativeManifest", "GetCopyToOutputDirectoryItems",
        "GetCopyToPublishDirectoryItems", "Build", "Clean", "Rebuild",
        "GetReferenceAssemblyPaths", "Compile", "Publish"
    };

    /// <summary>
    /// Gets the most expensive targets across the entire build, aggregated by name.
    /// </summary>
    public static IReadOnlyList<TargetExecutionData> GetExpensiveTargets(
        Build build,
        int limit = 20,
        int offset = 0)
    {
        var aggregated = new Dictionary<string, (int count, long totalExclusive, long totalInclusive, long min, long max)>(
            StringComparer.OrdinalIgnoreCase);

        build.VisitAllChildren<Target>(target =>
        {
            if (target.Skipped) return;

            var inclusiveMs = (long)target.Duration.TotalMilliseconds;
            var exclusiveMs = CalculateExclusiveDuration(target);

            if (aggregated.TryGetValue(target.Name, out var existing))
            {
                aggregated[target.Name] = (
                    existing.count + 1,
                    existing.totalExclusive + exclusiveMs,
                    existing.totalInclusive + inclusiveMs,
                    Math.Min(existing.min, exclusiveMs),
                    Math.Max(existing.max, exclusiveMs));
            }
            else
            {
                aggregated[target.Name] = (1, exclusiveMs, inclusiveMs, exclusiveMs, exclusiveMs);
            }
        });

        return aggregated
            .Select(kvp => new TargetExecutionData(
                kvp.Key,
                kvp.Value.count,
                kvp.Value.totalExclusive,
                kvp.Value.totalInclusive,
                kvp.Value.min,
                kvp.Value.max))
            .OrderByDescending(t => t.TotalExclusiveMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Search for targets by name across all projects.
    /// </summary>
    public static IReadOnlyList<TargetInfo> SearchTargets(
        Build build,
        string targetName,
        int limit = 50,
        int offset = 0)
    {
        var results = new List<TargetInfo>();

        build.VisitAllChildren<Target>(target =>
        {
            if (target.Name != null &&
                target.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            {
                var project = FindAncestor<Project>(target);
                results.Add(new TargetInfo(
                    target.Id,
                    target.Name,
                    project?.ProjectFile ?? "(unknown)",
                    project?.Id ?? -1,
                    (long)target.Duration.TotalMilliseconds,
                    target.Skipped,
                    target.DependsOnTargets,
                    FindAncestor<Target>(target)?.Name));
            }
        });

        return results
            .OrderByDescending(t => t.DurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get all targets for a specific project, with timing info.
    /// </summary>
    public static IReadOnlyList<TargetInfo> GetTargetsByProject(
        Build build,
        string projectFilter,
        int limit = 100,
        int offset = 0)
    {
        var results = new List<TargetInfo>();

        build.VisitAllChildren<Project>(project =>
        {
            if (project.ProjectFile == null ||
                !project.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var target in project.Children.OfType<Target>())
            {
                results.Add(new TargetInfo(
                    target.Id,
                    target.Name,
                    project.ProjectFile,
                    project.Id,
                    (long)target.Duration.TotalMilliseconds,
                    target.Skipped,
                    target.DependsOnTargets,
                    null));
            }
        });

        return results
            .OrderByDescending(t => t.DurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Calculates target exclusive duration by subtracting MSBuild task durations
    /// that invoke cross-project reference protocol calls.
    /// </summary>
    internal static long CalculateExclusiveDuration(Target target)
    {
        var inclusiveMs = (long)target.Duration.TotalMilliseconds;
        long msBuildTaskMs = 0;

        foreach (var task in target.Children.OfType<MSBuildTask>())
        {
            if (string.Equals(task.Name, "MSBuild", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(task.Name, "CallTarget", StringComparison.OrdinalIgnoreCase))
            {
                msBuildTaskMs += (long)task.Duration.TotalMilliseconds;
            }
        }

        return Math.Max(0, inclusiveMs - msBuildTaskMs);
    }

    private static TAncestor? FindAncestor<TAncestor>(BaseNode node)
        where TAncestor : BaseNode
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is TAncestor match)
                return match;
            current = (current as BaseNode)?.Parent;
        }
        return null;
    }
}
