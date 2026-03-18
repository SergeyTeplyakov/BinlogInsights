using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ExpensiveTargetsTool
{
    [McpServerTool(Name = "binlog_expensive_targets", Title = "Expensive Targets",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the most expensive MSBuild targets across the build, aggregated by name and ordered by exclusive duration. Exclusive duration subtracts time spent in cross-project MSBuild/CallTarget calls.")]
    public static List<object> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Maximum number of targets to return (default: 20)")] int top_number = 20,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return TargetPerformanceQuery.GetExpensiveTargets(build, top_number, offset)
            .Select(t => (object)new
            {
                name = t.TargetName,
                duration = FormatDuration(t.TotalExclusiveMs, t.ExecutionCount),
                executionCount = t.ExecutionCount,
                totalExclusiveMs = t.TotalExclusiveMs,
                totalInclusiveMs = t.TotalInclusiveMs,
                minDurationMs = t.MinDurationMs,
                maxDurationMs = t.MaxDurationMs,
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
public class SearchTargetsTool
{
    [McpServerTool(Name = "binlog_search_targets", Title = "Search Targets",
        ReadOnly = true, Idempotent = true)]
    [Description("Search for MSBuild targets by name across all projects. Returns matching targets with project context and duration.")]
    public static IReadOnlyList<TargetInfo> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Target name substring to search for")] string target_name,
        [Description("Maximum number of results (default: 50)")] int limit = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return TargetPerformanceQuery.SearchTargets(build, target_name, limit, offset);
    }
}

[McpServerToolType]
public class ProjectTargetsTool
{
    [McpServerTool(Name = "binlog_project_targets", Title = "Project Targets",
        ReadOnly = true, Idempotent = true)]
    [Description("Get all targets for a specific project with inclusive and exclusive timing. Use to understand what a project build actually does.")]
    public static IReadOnlyList<TargetInfo> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to filter by project name")] string project,
        [Description("Maximum number of results (default: 100)")] int limit = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return TargetPerformanceQuery.GetTargetsByProject(build, project, limit, offset);
    }
}
