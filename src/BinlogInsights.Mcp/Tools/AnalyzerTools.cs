using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ExpensiveAnalyzersTool
{
    [McpServerTool(Name = "binlog_expensive_analyzers", Title = "Expensive Analyzers",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the most expensive Roslyn analyzers and source generators across the entire build, aggregated by name. Useful for identifying slow analyzers that impact build performance.")]
    public static IReadOnlyList<AggregatedAnalyzerData> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Maximum number of analyzers to return (default: 20)")] int limit = 20,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return AnalyzerQuery.GetExpensiveAnalyzers(build, limit, offset);
    }
}
