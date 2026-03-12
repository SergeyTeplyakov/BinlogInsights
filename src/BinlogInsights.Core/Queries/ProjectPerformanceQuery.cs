using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class ProjectPerformanceQuery
{
    /// <summary>
    /// Gets the most expensive projects by exclusive target duration.
    /// </summary>
    public static IReadOnlyList<ProjectPerformanceData> GetExpensiveProjects(
        Build build,
        int limit = 20,
        int offset = 0)
    {
        var results = new List<ProjectPerformanceData>();

        foreach (var project in build.Children.OfType<Project>())
        {
            var targets = project.Children.OfType<Target>().Where(t => !t.Skipped).ToList();
            long totalInclusive = 0;
            long totalExclusive = 0;

            foreach (var target in targets)
            {
                totalInclusive += (long)target.Duration.TotalMilliseconds;
                totalExclusive += TargetPerformanceQuery.CalculateExclusiveDuration(target);
            }

            results.Add(new ProjectPerformanceData(
                project.ProjectFile ?? "(unknown)",
                project.Id,
                totalExclusive,
                totalInclusive,
                targets.Count));
        }

        return results
            .OrderByDescending(p => p.ExclusiveDurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets detailed target timing for a specific project.
    /// </summary>
    public static IReadOnlyList<ProjectTargetData> GetProjectTargets(
        Build build,
        string projectFilter,
        int limit = 100,
        int offset = 0)
    {
        var results = new List<ProjectTargetData>();

        foreach (var project in build.Children.OfType<Project>())
        {
            if (project.ProjectFile == null ||
                !project.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var target in project.Children.OfType<Target>())
            {
                var inclusiveMs = (long)target.Duration.TotalMilliseconds;
                var exclusiveMs = target.Skipped ? 0 : TargetPerformanceQuery.CalculateExclusiveDuration(target);

                results.Add(new ProjectTargetData(
                    target.Id,
                    target.Name,
                    inclusiveMs,
                    exclusiveMs,
                    target.Skipped));
            }
        }

        return results
            .OrderByDescending(t => t.ExclusiveDurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }
}
