using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class PropertiesTool
{
    [McpServerTool(Name = "binlog_properties", Title = "MSBuild Properties",
        ReadOnly = true, Idempotent = true)]
    [Description("Get evaluated MSBuild properties for a project. Use to check TargetFramework, OutputPath, environment variables, or any property value. Without a filter, shows a curated set of important properties.")]
    public static IReadOnlyList<PropertyResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to match the project name (required)")] string project,
        [Description("Optional comma-separated property names or substring to filter (e.g. 'TargetFramework,OutputPath')")] string? filter = null)
    {
        var build = cache.Load(binlog_file);
        return PropertiesQuery.Execute(build, project, filter);
    }
}
