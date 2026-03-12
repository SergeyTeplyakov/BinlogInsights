using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class OverviewTool
{
    [McpServerTool(Name = "binlog_overview", Title = "Build Overview",
        ReadOnly = true, Idempotent = true)]
    [Description("Get a high-level overview of a build from a .binlog file: success/failure, duration, project list, error/warning counts. Start here when investigating a build.")]
    public static BuildOverviewResult Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file)
    {
        var build = cache.Load(binlog_file);
        return BuildOverviewQuery.Execute(build);
    }
}
