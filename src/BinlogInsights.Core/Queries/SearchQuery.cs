using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogInsights.Core.Queries;

public static class SearchQuery
{
    public static IReadOnlyList<SearchResult> Execute(
        Build build,
        string query,
        string? projectFilter = null,
        int limit = 50,
        int offset = 0)
    {
        var results = new List<SearchResult>();

        build.VisitAllChildren<Message>(message =>
        {
            var text = message.Text ?? message.ToString();
            if (text == null || !text.Contains(query, StringComparison.OrdinalIgnoreCase))
                return;

            var projectFile = FindAncestorProjectFile(message);
            if (!MatchesProjectFilter(projectFile, projectFilter))
                return;

            results.Add(new SearchResult(
                Message: text,
                ProjectFile: projectFile,
                TargetName: FindAncestorName<Target>(message),
                TaskName: FindAncestorName<MSBuildTask>(message),
                NodeType: "Message"));
        });

        // Also search errors and warnings
        build.VisitAllChildren<AbstractDiagnostic>(diagnostic =>
        {
            var text = diagnostic.Text ?? diagnostic.ToString();
            if (text == null || !text.Contains(query, StringComparison.OrdinalIgnoreCase))
                return;

            var projectFile = FindAncestorProjectFile(diagnostic);
            if (!MatchesProjectFilter(projectFile, projectFilter))
                return;

            var nodeType = diagnostic is Error ? "Error" : "Warning";
            results.Add(new SearchResult(
                Message: text,
                ProjectFile: projectFile,
                TargetName: FindAncestorName<Target>(diagnostic),
                TaskName: FindAncestorName<MSBuildTask>(diagnostic),
                NodeType: nodeType));
        });

        // Search properties (name and value)
        build.VisitAllChildren<Property>(property =>
        {
            var nameMatch = property.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            var valueMatch = property.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            if (!nameMatch && !valueMatch)
                return;

            var projectFile = FindAncestorProjectFile(property);
            if (!MatchesProjectFilter(projectFile, projectFilter))
                return;

            results.Add(new SearchResult(
                Message: $"{property.Name} = {property.Value}",
                ProjectFile: projectFile,
                TargetName: null,
                TaskName: null,
                NodeType: "Property"));
        });

        // Search items (item spec / include path)
        build.VisitAllChildren<Item>(item =>
        {
            var text = item.Text ?? item.Name;
            if (text == null || !text.Contains(query, StringComparison.OrdinalIgnoreCase))
                return;

            var projectFile = FindAncestorProjectFile(item);
            if (!MatchesProjectFilter(projectFile, projectFilter))
                return;

            var itemType = FindAncestorName<AddItem>(item)
                        ?? FindAncestorName<Folder>(item);

            results.Add(new SearchResult(
                Message: $"{itemType}: {text}",
                ProjectFile: projectFile,
                TargetName: FindAncestorName<Target>(item),
                TaskName: FindAncestorName<MSBuildTask>(item),
                NodeType: "Item"));
        });

        // Search item metadata (name and value)
        build.VisitAllChildren<Metadata>(metadata =>
        {
            var nameMatch = metadata.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            var valueMatch = metadata.Value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
            if (!nameMatch && !valueMatch)
                return;

            var projectFile = FindAncestorProjectFile(metadata);
            if (!MatchesProjectFilter(projectFile, projectFilter))
                return;

            // Find the parent item spec for context
            var parentItem = metadata.Parent as Item;
            var itemType = FindAncestorName<AddItem>(metadata)
                        ?? FindAncestorName<Folder>(metadata);
            var itemSpec = parentItem?.Text ?? parentItem?.Name;
            var context = itemType != null && itemSpec != null
                ? $"{itemType}: {itemSpec} | {metadata.Name} = {metadata.Value}"
                : $"{metadata.Name} = {metadata.Value}";

            results.Add(new SearchResult(
                Message: context,
                ProjectFile: projectFile,
                TargetName: FindAncestorName<Target>(metadata),
                TaskName: FindAncestorName<MSBuildTask>(metadata),
                NodeType: "Metadata"));
        });

        return results.Skip(offset).Take(limit).ToList();
    }

    private static bool MatchesProjectFilter(string? projectFile, string? projectFilter)
    {
        return string.IsNullOrEmpty(projectFilter)
            || (projectFile != null && projectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindAncestorProjectFile(BaseNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is Project project)
                return project.ProjectFile;
            if (current is ProjectEvaluation evaluation)
                return evaluation.ProjectFile;
            current = (current as BaseNode)?.Parent;
        }
        return null;
    }

    private static string? FindAncestorName<T>(BaseNode node) where T : NamedNode
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is T named)
                return named.Name;
            current = (current as BaseNode)?.Parent;
        }
        return null;
    }
}
