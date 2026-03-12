using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogInsights.Core.Queries;

public static class TaskPerformanceQuery
{
    /// <summary>
    /// Gets the most expensive tasks across the entire build, aggregated by task name.
    /// </summary>
    public static IReadOnlyList<TaskExecutionData> GetExpensiveTasks(
        Build build,
        int limit = 20,
        int offset = 0)
    {
        var aggregated = new Dictionary<string, List<long>>(StringComparer.OrdinalIgnoreCase);

        build.VisitAllChildren<MSBuildTask>(task =>
        {
            var durationMs = (long)task.Duration.TotalMilliseconds;
            if (!aggregated.TryGetValue(task.Name, out var durations))
            {
                durations = [];
                aggregated[task.Name] = durations;
            }
            durations.Add(durationMs);
        });

        return aggregated
            .Select(kvp =>
            {
                var durations = kvp.Value;
                return new TaskExecutionData(
                    kvp.Key,
                    durations.Count,
                    durations.Sum(),
                    durations.Min(),
                    durations.Max(),
                    durations.Sum() / durations.Count);
            })
            .OrderByDescending(t => t.TotalDurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Searches for tasks by name across all projects.
    /// </summary>
    public static IReadOnlyList<TaskInfo> SearchTasks(
        Build build,
        string taskName,
        int limit = 50,
        int offset = 0)
    {
        var results = new List<TaskInfo>();

        build.VisitAllChildren<MSBuildTask>(task =>
        {
            if (task.Name != null &&
                task.Name.Contains(taskName, StringComparison.OrdinalIgnoreCase))
            {
                var project = FindAncestor<Project>(task);
                var target = FindAncestor<Target>(task);

                results.Add(new TaskInfo(
                    task.Id,
                    task.Name,
                    project?.ProjectFile ?? "(unknown)",
                    target?.Name ?? "(unknown)",
                    (long)task.Duration.TotalMilliseconds));
            }
        });

        return results
            .OrderByDescending(t => t.DurationMs)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// List tasks within a specific target of a project.
    /// </summary>
    public static IReadOnlyList<TaskInfo> GetTasksInTarget(
        Build build,
        string projectFilter,
        string targetName,
        int limit = 100,
        int offset = 0)
    {
        var results = new List<TaskInfo>();

        build.VisitAllChildren<Target>(target =>
        {
            if (!string.Equals(target.Name, targetName, StringComparison.OrdinalIgnoreCase))
                return;

            var project = FindAncestor<Project>(target);
            if (project?.ProjectFile == null ||
                !project.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var task in target.Children.OfType<MSBuildTask>())
            {
                results.Add(new TaskInfo(
                    task.Id,
                    task.Name,
                    project.ProjectFile,
                    target.Name,
                    (long)task.Duration.TotalMilliseconds));
            }
        });

        return results
            .OrderBy(t => t.Id)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets detailed info about a specific task execution.
    /// </summary>
    public static TaskDetails? GetTaskDetails(
        Build build,
        string projectFilter,
        string targetName,
        string taskName)
    {
        TaskDetails? result = null;

        build.VisitAllChildren<MSBuildTask>(task =>
        {
            if (result != null) return;
            if (!string.Equals(task.Name, taskName, StringComparison.OrdinalIgnoreCase))
                return;

            var target = FindAncestor<Target>(task);
            if (target == null || !string.Equals(target.Name, targetName, StringComparison.OrdinalIgnoreCase))
                return;

            var project = FindAncestor<Project>(task);
            if (project?.ProjectFile == null ||
                !project.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
                return;

            var parameters = new Dictionary<string, string>();
            foreach (var param in task.Children.OfType<Property>())
            {
                parameters[param.Name] = param.Value ?? "";
            }
            // Also check for Folder named "Parameters"
            var paramFolder = task.Children.OfType<Folder>()
                .FirstOrDefault(f => string.Equals(f.Name, "Parameters", StringComparison.OrdinalIgnoreCase));
            if (paramFolder != null)
            {
                foreach (var param in paramFolder.Children.OfType<Property>())
                {
                    parameters[param.Name] = param.Value ?? "";
                }
            }

            var messages = new List<string>();
            foreach (var msg in task.Children.OfType<Message>())
            {
                if (msg.Text != null)
                    messages.Add(msg.Text);
            }

            result = new TaskDetails(
                task.Id,
                task.Name,
                project.ProjectFile,
                target.Name,
                (long)task.Duration.TotalMilliseconds,
                parameters,
                messages);
        });

        return result;
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
