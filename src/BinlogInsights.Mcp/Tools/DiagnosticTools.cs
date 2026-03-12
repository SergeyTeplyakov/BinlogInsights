using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ErrorsTool
{
    [McpServerTool(Name = "binlog_errors", Title = "Build Errors",
        ReadOnly = true, Idempotent = true)]
    [Description("Get build errors with full project/target/task context. Use after binlog_overview shows errors.")]
    public static IReadOnlyList<DiagnosticResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Optional substring to filter by project name")] string? project = null,
        [Description("Maximum number of results (default: 50)")] int limit = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return DiagnosticsQuery.GetErrors(build, project, limit, offset);
    }
}

[McpServerToolType]
public class WarningsTool
{
    [McpServerTool(Name = "binlog_warnings", Title = "Build Warnings",
        ReadOnly = true, Idempotent = true)]
    [Description("Get build warnings with context. Optionally filter by warning code (e.g. CS0618, NU1603).")]
    public static IReadOnlyList<DiagnosticResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Optional substring to filter by project name")] string? project = null,
        [Description("Optional warning code to filter (e.g. CS0618, NU1603)")] string? code = null,
        [Description("Maximum number of results (default: 50)")] int limit = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return DiagnosticsQuery.GetWarnings(build, project, code, limit, offset);
    }
}
