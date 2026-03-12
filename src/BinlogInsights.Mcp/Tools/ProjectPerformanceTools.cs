using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ExpensiveProjectsTool
{
    [McpServerTool(Name = "binlog_expensive_projects", Title = "Expensive Projects",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the most expensive projects by exclusive target duration. Exclusive duration is the actual work done in each project, excluding time spent calling into other projects.")]
    public static IReadOnlyList<ProjectPerformanceData> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Maximum number of projects to return (default: 20)")] int limit = 20,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return ProjectPerformanceQuery.GetExpensiveProjects(build, limit, offset);
    }
}

[McpServerToolType]
public class ProjectTargetTimesTool
{
    [McpServerTool(Name = "binlog_project_target_times", Title = "Project Target Times",
        ReadOnly = true, Idempotent = true)]
    [Description("Get detailed target-level timing for a specific project. Shows both inclusive and exclusive duration for each target.")]
    public static IReadOnlyList<ProjectTargetData> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to filter by project name")] string project,
        [Description("Maximum number of results (default: 100)")] int limit = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return ProjectPerformanceQuery.GetProjectTargets(build, project, limit, offset);
    }
}
