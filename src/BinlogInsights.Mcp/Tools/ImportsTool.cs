using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ImportsTool
{
    [McpServerTool(Name = "binlog_imports", Title = "Project Import Tree",
        ReadOnly = true, Idempotent = true)]
    [Description("Show the full import tree for a project: Directory.Build.props, SDK imports, missing imports. Use to diagnose import-driven issues or unexpected property overrides.")]
    public static IReadOnlyList<ImportResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to match the project name (required)")] string project)
    {
        var build = cache.Load(binlog_file);
        return ImportsQuery.Execute(build, project);
    }
}
