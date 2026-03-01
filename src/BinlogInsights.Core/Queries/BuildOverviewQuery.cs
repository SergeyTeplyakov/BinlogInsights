using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class BuildOverviewQuery
{
    public static BuildOverviewResult Execute(Build build)
    {
        var projects = new List<ProjectSummary>();

        // Collect top-level project invocations
        foreach (var project in build.Children.OfType<Project>())
        {
            // Project doesn't have Succeeded; check whether it contains any Error children
            var hasErrors = false;
            project.VisitAllChildren<Error>(_ => hasErrors = true);

            projects.Add(new ProjectSummary(
                ProjectFile: project.ProjectFile ?? "(unknown)",
                TargetFramework: project.TargetFramework,
                Succeeded: !hasErrors,
                Duration: project.Duration));
        }

        var errorCount = 0;
        var warningCount = 0;
        build.VisitAllChildren<Error>(_ => errorCount++);
        build.VisitAllChildren<Warning>(_ => warningCount++);

        return new BuildOverviewResult(
            Succeeded: build.Succeeded,
            MsBuildVersion: build.MSBuildVersion,
            Duration: build.Duration,
            ProjectCount: projects.Count,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Projects: projects);
    }
}
