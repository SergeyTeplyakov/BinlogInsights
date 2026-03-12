using System.ComponentModel;
using BinlogInsights.Core.Models;
using BinlogInsights.Core.Queries;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

[McpServerToolType]
public class ItemsTool
{
    [McpServerTool(Name = "binlog_items", Title = "MSBuild Items",
        ReadOnly = true, Idempotent = true)]
    [Description("Get MSBuild items of a specific type (e.g. Compile, PackageReference, Reference, ProjectReference). Use to check what packages, files, or references a project has.")]
    public static IReadOnlyList<ItemResult> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to match the project name (required)")] string project,
        [Description("Item type to retrieve (e.g. PackageReference, Compile, Reference)")] string item_type,
        [Description("Maximum number of results (default: 100)")] int limit = 100,
        [Description("Number of results to skip for pagination (default: 0)")] int offset = 0)
    {
        var build = cache.Load(binlog_file);
        return ItemsQuery.Execute(build, project, item_type, limit, offset);
    }
}

[McpServerToolType]
public class ItemTypesTool
{
    [McpServerTool(Name = "binlog_item_types", Title = "List Item Types",
        ReadOnly = true, Idempotent = true)]
    [Description("List all available MSBuild item types for a project. Use to discover what item types exist before querying with binlog_items.")]
    public static IReadOnlyList<string> Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Substring to match the project name (required)")] string project)
    {
        var build = cache.Load(binlog_file);
        return ItemsQuery.GetItemTypes(build, project);
    }
}
