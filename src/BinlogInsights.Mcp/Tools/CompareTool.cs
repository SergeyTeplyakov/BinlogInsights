using System.ComponentModel;
using BinlogInsights.Core;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class CompareTool
{
    [McpServerTool(Name = "binlog_compare", Title = "Compare Two Builds",
        ReadOnly = true, Idempotent = true)]
    [Description("Compare two .binlog files to find differences in properties, imports, compiler references, and NuGet packages. Use when investigating why the same project builds differently in two environments.")]
    public static CompareResult Execute(
        BinlogCache cache,
        [Description("Path to the first .binlog file (baseline)")] string binlog_file_a,
        [Description("Path to the second .binlog file (comparison)")] string binlog_file_b,
        [Description("Optional substring to filter by project name")] string? project = null)
    {
        var buildA = cache.Load(binlog_file_a);
        var buildB = cache.Load(binlog_file_b);
        return CompareQuery.Execute(buildA, buildB, project);
    }
}
