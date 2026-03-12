using System.ComponentModel;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class PreprocessTool
{
    [McpServerTool(Name = "binlog_preprocess", Title = "Preprocessed Project XML",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the effective project XML after all imports are inlined. Shows the final merged view of a project as MSBuild sees it. Useful for understanding complex import chains.")]
    public static string Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to match the project name (required)")] string project,
        [Description("Maximum character length of output (default: 30000)")] int max_length = 30_000)
    {
        var build = cache.Load(binlog_file);
        return PreprocessQuery.Execute(build, project, max_length) ?? "No matching project found.";
    }
}
