using System.ComponentModel;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ListFilesTool
{
    [McpServerTool(Name = "binlog_list_files", Title = "List Embedded Files",
        ReadOnly = true, Idempotent = true)]
    [Description("List source files embedded in the binlog. Binary logs can contain project files, props, targets, and other source files from the build. Use to discover available files before retrieving their content.")]
    public static IReadOnlyList<string> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Optional path substring to filter files")] string? path_filter = null,
        [Description("Maximum number of files to return (default: 100)")] int limit = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return FileQuery.ListFiles(build, path_filter, limit, offset);
    }
}

[McpServerToolType]
public class GetFileTool
{
    [McpServerTool(Name = "binlog_get_file", Title = "Get Embedded File",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the content of a source file embedded in the binlog. Use binlog_list_files first to find available file paths.")]
    public static string Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Full path of the file inside the binlog (from binlog_list_files)")] string file_path)
    {
        var build = cache.Load(binlog_file);
        return FileQuery.GetFileContent(build, file_path)
            ?? $"File not found in binlog: {file_path}";
    }
}
