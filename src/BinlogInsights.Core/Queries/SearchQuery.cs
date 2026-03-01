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
            if (!string.IsNullOrEmpty(projectFilter) &&
                (projectFile == null || !projectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
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
            if (!string.IsNullOrEmpty(projectFilter) &&
                (projectFile == null || !projectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
                return;

            var nodeType = diagnostic is Error ? "Error" : "Warning";
            results.Add(new SearchResult(
                Message: text,
                ProjectFile: projectFile,
                TargetName: FindAncestorName<Target>(diagnostic),
                TaskName: FindAncestorName<MSBuildTask>(diagnostic),
                NodeType: nodeType));
        });

        return results.Skip(offset).Take(limit).ToList();
    }

    private static string? FindAncestorProjectFile(BaseNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is Project project)
                return project.ProjectFile;
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
