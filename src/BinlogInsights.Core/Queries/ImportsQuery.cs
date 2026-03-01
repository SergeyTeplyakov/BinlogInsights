using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class ImportsQuery
{
    public static IReadOnlyList<ImportResult> Execute(Build build, string projectFilter)
    {
        var evaluation = FindEvaluation(build, projectFilter);
        if (evaluation == null)
            return [];

        // Look for the Imports folder under evaluation
        var importsFolder = evaluation.Children
            .OfType<TreeNode>()
            .FirstOrDefault(n => n is NamedNode named && named.Name == "Imports");

        if (importsFolder == null)
        {
            // Try looking at all children for Import nodes directly
            var directImports = new List<ImportResult>();
            CollectImports(evaluation, directImports);
            return directImports;
        }

        var results = new List<ImportResult>();
        CollectImports(importsFolder, results);
        return results;
    }

    private static void CollectImports(TreeNode parent, List<ImportResult> results)
    {
        foreach (var child in parent.Children)
        {
            if (child is Import import)
            {
                var children = new List<ImportResult>();
                CollectImports(import, children);
                results.Add(new ImportResult(
                    ImportedFile: import.ImportedProjectFilePath ?? import.Text ?? "(unknown)",
                    ImportedBy: import.ProjectFilePath,
                    Line: import.Line > 0 ? import.Line : null,
                    Column: import.Column > 0 ? import.Column : null,
                    IsMissing: false,
                    Children: children));
            }
            else if (child is NoImport noImport)
            {
                results.Add(new ImportResult(
                    ImportedFile: noImport.ImportedFileSpec ?? noImport.Text ?? "(unknown)",
                    ImportedBy: noImport.ProjectFilePath,
                    Line: noImport.Line > 0 ? noImport.Line : null,
                    Column: noImport.Column > 0 ? noImport.Column : null,
                    IsMissing: true,
                    Children: []));
            }
            else if (child is TreeNode treeNode)
            {
                CollectImports(treeNode, results);
            }
        }
    }

    internal static ProjectEvaluation? FindEvaluation(Build build, string projectFilter)
    {
        ProjectEvaluation? result = null;

        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            if (result != null) return;
            if (eval.ProjectFile != null &&
                eval.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase))
            {
                result = eval;
            }
        });

        return result;
    }
}
