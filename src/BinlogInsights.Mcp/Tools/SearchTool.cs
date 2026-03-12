using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class SearchTool
{
    [McpServerTool(Name = "binlog_search", Title = "Search Build Messages",
        ReadOnly = true, Idempotent = true)]
    [Description("Free-text search across all build messages, diagnostics, properties, items, and metadata. Use for general investigation when you don't know exactly what to look for.")]
    public static IReadOnlyList<SearchResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Text to search for (case-insensitive)")] string query,
        [Description("Optional substring to filter by project name")] string? project = null,
        [Description("Maximum number of results (default: 50)")] int limit = 50,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return SearchQuery.Execute(build, query, project, limit, offset);
    }
}
