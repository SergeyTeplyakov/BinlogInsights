using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

using MSBuildTask = Microsoft.Build.Logging.StructuredLogger.Task;

namespace BinlogInsights.Core.Queries;

public static class DiagnosticsQuery
{
    public static IReadOnlyList<DiagnosticResult> GetErrors(
        Build build,
        string? projectFilter = null,
        int limit = 50,
        int offset = 0)
    {
        return GetDiagnostics<Error>(build, "error", projectFilter, limit, offset);
    }

    public static IReadOnlyList<DiagnosticResult> GetWarnings(
        Build build,
        string? projectFilter = null,
        string? warningCode = null,
        int limit = 50,
        int offset = 0)
    {
        var results = GetDiagnostics<Warning>(build, "warning", projectFilter, int.MaxValue, 0);

        if (!string.IsNullOrEmpty(warningCode))
        {
            results = results
                .Where(d => string.Equals(d.Code, warningCode, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return results.Skip(offset).Take(limit).ToList();
    }

    public static int CountErrors(Build build)
    {
        var count = 0;
        build.VisitAllChildren<Error>(_ => count++);
        return count;
    }

    public static int CountWarnings(Build build)
    {
        var count = 0;
        build.VisitAllChildren<Warning>(_ => count++);
        return count;
    }

    private static List<DiagnosticResult> GetDiagnostics<T>(
        Build build,
        string severity,
        string? projectFilter,
        int limit,
        int offset)
        where T : AbstractDiagnostic
    {
        var diagnostics = new List<DiagnosticResult>();

        build.VisitAllChildren<T>(diagnostic =>
        {
            var projectFile = FindAncestor<Project>(diagnostic)?.ProjectFile;

            if (!string.IsNullOrEmpty(projectFilter) &&
                (projectFile == null || !projectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            diagnostics.Add(new DiagnosticResult(
                Severity: severity,
                Code: diagnostic.Code,
                Message: diagnostic.Text ?? diagnostic.ToString(),
                File: diagnostic.File,
                Line: diagnostic.LineNumber > 0 ? diagnostic.LineNumber : null,
                Column: diagnostic.ColumnNumber > 0 ? diagnostic.ColumnNumber : null,
                ProjectFile: projectFile,
                TargetName: FindAncestor<Target>(diagnostic)?.Name,
                TaskName: FindAncestor<MSBuildTask>(diagnostic)?.Name));
        });

        return diagnostics.Skip(offset).Take(limit).ToList();
    }

    private static TAncestor? FindAncestor<TAncestor>(BaseNode node)
        where TAncestor : BaseNode
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is TAncestor match)
                return match;
            current = (current as BaseNode)?.Parent;
        }
        return null;
    }
}
