using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ProjectsTool
{
    [McpServerTool(Name = "binlog_projects", Title = "List Projects",
        ReadOnly = true, Idempotent = true)]
    [Description("List all projects in the binlog with their full paths and whether they are legacy (non-SDK-style) projects.")]
    public static IReadOnlyList<ProjectFileInfo> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file)
    {
        var build = cache.Load(binlog_file);
        return ProjectsQuery.GetProjectFiles(build);
    }
}
