using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class EvaluationQuery
{
    /// <summary>
    /// Lists all project evaluations, optionally filtered by project path.
    /// </summary>
    public static IReadOnlyList<EvaluationData> GetEvaluations(
        Build build,
        string? projectFilter = null)
    {
        var results = new List<EvaluationData>();

        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            if (!string.IsNullOrEmpty(projectFilter) &&
                (eval.ProjectFile == null ||
                 !eval.ProjectFile.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            results.Add(new EvaluationData(
                eval.Id,
                eval.ProjectFile ?? "(unknown)",
                (long)eval.Duration.TotalMilliseconds));
        });

        return results.OrderByDescending(e => e.DurationMs).ToList();
    }

    /// <summary>
    /// Gets the global properties for a specific evaluation by ID.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetEvaluationGlobalProperties(
        Build build,
        int evaluationId)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ProjectEvaluation? eval = null;

        build.VisitAllChildren<ProjectEvaluation>(e =>
        {
            if (eval != null) return;
            if (e.Id == evaluationId)
                eval = e;
        });

        if (eval == null)
            return result;

        var propertiesFolder = eval.Children
            .OfType<Folder>()
            .FirstOrDefault(f => f.Name == "Properties");

        if (propertiesFolder == null)
            return result;

        var globalFolder = propertiesFolder.Children
            .OfType<Folder>()
            .FirstOrDefault(f => f.Name == "Global");

        if (globalFolder == null)
            return result;

        foreach (var prop in globalFolder.Children.OfType<Property>())
        {
            result[prop.Name] = prop.Value ?? "";
        }

        return result;
    }

    /// <summary>
    /// Gets all properties for a specific evaluation, optionally filtering by name.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetEvaluationProperties(
        Build build,
        int evaluationId,
        string[]? propertyNames = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ProjectEvaluation? eval = null;

        build.VisitAllChildren<ProjectEvaluation>(e =>
        {
            if (eval != null) return;
            if (e.Id == evaluationId)
                eval = e;
        });

        if (eval == null)
            return result;

        var propertiesFolder = eval.Children
            .OfType<Folder>()
            .FirstOrDefault(f => f.Name == "Properties");

        if (propertiesFolder == null)
            return result;

        var nameSet = propertyNames != null && propertyNames.Length > 0
            ? new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase)
            : null;

        void CollectProperties(TreeNode parent)
        {
            foreach (var child in parent.Children)
            {
                if (child is Property prop)
                {
                    if (nameSet == null || nameSet.Contains(prop.Name))
                        result[prop.Name] = prop.Value ?? "";
                }
                else if (child is Folder folder)
                {
                    CollectProperties(folder);
                }
            }
        }

        CollectProperties(propertiesFolder);
        return result;
    }
}
