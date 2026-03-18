using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ExpensiveTasksTool
{
    [McpServerTool(Name = "binlog_expensive_tasks", Title = "Expensive Tasks",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the most expensive MSBuild tasks across the build, aggregated by name. Shows total, min, max, and average duration.")]
    public static List<object> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Maximum number of tasks to return (default: 20)")] int top_number = 20,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return TaskPerformanceQuery.GetExpensiveTasks(build, top_number, offset)
            .Select(t => (object)new
            {
                name = t.TaskName,
                duration = FormatDuration(t.TotalDurationMs, t.ExecutionCount),
                executionCount = t.ExecutionCount,
                totalDurationMs = t.TotalDurationMs,
                minDurationMs = t.MinDurationMs,
                maxDurationMs = t.MaxDurationMs,
                avgDurationMs = t.AvgDurationMs,
            })
            .ToList();
    }

    private static string FormatDuration(long ms, int count)
    {
        var dur = ms >= 1000 ? $"{ms / 1000.0:F1}s" : $"{ms}ms";
        return count > 1 ? $"{dur} (\u00d7{count})" : dur;
    }
}

[McpServerToolType]
public class SearchTasksTool
{
    [McpServerTool(Name = "binlog_search_tasks", Title = "Search Tasks",
        ReadOnly = true, Idempotent = true)]
    [Description("Search for MSBuild tasks by name across all projects. Returns matching tasks with project and target context.")]
    public static IReadOnlyList<TaskInfo> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Task name substring to search for")] string task_name,
        [Description("Maximum number of results (default: 50)")] int limit = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return TaskPerformanceQuery.SearchTasks(build, task_name, limit, offset);
    }
}

[McpServerToolType]
public class TasksInTargetTool
{
    [McpServerTool(Name = "binlog_tasks_in_target", Title = "Tasks in Target",
        ReadOnly = true, Idempotent = true)]
    [Description("List all tasks within a specific target of a project. Use after identifying an expensive target to see what tasks it runs.")]
    public static IReadOnlyList<TaskInfo> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to filter by project name")] string project,
        [Description("Exact target name")] string target_name,
        [Description("Maximum number of results (default: 100)")] int limit = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return TaskPerformanceQuery.GetTasksInTarget(build, project, target_name, limit, offset);
    }
}

[McpServerToolType]
public class TaskDetailsTool
{
    [McpServerTool(Name = "binlog_task_details", Title = "Task Details",
        ReadOnly = true, Idempotent = true)]
    [Description("Get detailed info about a specific task execution including its parameters and output messages.")]
    public static TaskDetails? Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to filter by project name")] string project,
        [Description("Exact target name containing the task")] string target_name,
        [Description("Exact task name")] string task_name)
    {
        var build = cache.Load(binlog_file);
        return TaskPerformanceQuery.GetTaskDetails(build, project, target_name, task_name);
    }
}
