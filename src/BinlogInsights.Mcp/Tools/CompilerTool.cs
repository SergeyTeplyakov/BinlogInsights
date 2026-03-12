using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class CompilerTool
{
    [McpServerTool(Name = "binlog_compiler", Title = "Compiler Invocations",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the exact csc/vbc compiler command-line invocations. Use to see compiler arguments, references passed to the compiler, and language version.")]
    public static IReadOnlyList<CompilerInvocationResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Optional substring to filter by project name")] string? project = null)
    {
        var build = cache.Load(binlog_file);
        return CompilerInvocationQuery.Execute(build, project);
    }
}
