using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class EvaluationsTool
{
    [McpServerTool(Name = "binlog_evaluations", Title = "Project Evaluations",
        ReadOnly = true, Idempotent = true)]
    [Description("List all project evaluations in the build. Evaluations happen before build execution and determine properties, items, and imports. Filter by project path to see evaluations for a specific project.")]
    public static IReadOnlyList<EvaluationData> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Optional substring to filter by project name")] string? project = null)
    {
        var build = cache.Load(binlog_file);
        return EvaluationQuery.GetEvaluations(build, project);
    }
}

[McpServerToolType]
public class EvaluationGlobalPropertiesTool
{
    [McpServerTool(Name = "binlog_evaluation_global_properties", Title = "Evaluation Global Properties",
        ReadOnly = true, Idempotent = true)]
    [Description("Get the global properties for a specific project evaluation. Global properties are what make evaluations distinct from one another (e.g. TargetFramework). Use binlog_evaluations first to find evaluation IDs.")]
    public static IReadOnlyDictionary<string, string> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("The evaluation ID (from binlog_evaluations)")] int evaluation_id)
    {
        var build = cache.Load(binlog_file);
        return EvaluationQuery.GetEvaluationGlobalProperties(build, evaluation_id);
    }
}

[McpServerToolType]
public class EvaluationPropertiesTool
{
    [McpServerTool(Name = "binlog_evaluation_properties", Title = "Evaluation Properties",
        ReadOnly = true, Idempotent = true)]
    [Description("Get properties from a specific project evaluation. Can optionally filter by property names. Use binlog_evaluations first to find evaluation IDs.")]
    public static IReadOnlyDictionary<string, string> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("The evaluation ID (from binlog_evaluations)")] int evaluation_id,
        [Description("Optional comma-separated property names to filter (e.g. 'TargetFramework,OutputPath')")] string? property_names = null)
    {
        var build = cache.Load(binlog_file);
        var names = string.IsNullOrEmpty(property_names)
            ? null
            : property_names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return EvaluationQuery.GetEvaluationProperties(build, evaluation_id, names);
    }
}
