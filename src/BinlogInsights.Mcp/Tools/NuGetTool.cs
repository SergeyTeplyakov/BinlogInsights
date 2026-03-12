using System.ComponentModel;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class NuGetTool
{
    [McpServerTool(Name = "binlog_nuget", Title = "NuGet Restore Diagnostics",
        ReadOnly = true, Idempotent = true)]
    [Description("Get NuGet restore diagnostics: package references, restore status, errors, and NuGet-related properties. Use for NU1101, NU1102, and other restore failures.")]
    public static NuGetRestoreResult Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Optional substring to filter by project name")] string? project = null)
    {
        var build = cache.Load(binlog_file);
        return NuGetRestoreQuery.Execute(build, project);
    }
}
