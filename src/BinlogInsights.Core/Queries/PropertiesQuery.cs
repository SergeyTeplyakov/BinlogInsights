using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class PropertiesQuery
{
    private static readonly string[] CuratedProperties =
    [
        "TargetFramework",
        "TargetFrameworks",
        "OutputPath",
        "IntermediateOutputPath",
        "BaseIntermediateOutputPath",
        "Configuration",
        "Platform",
        "RootNamespace",
        "AssemblyName",
        "NuGetPackageRoot",
        "MSBuildSDKsPath",
        "GenerateDocumentationFile",
        "Nullable",
        "LangVersion",
        "TreatWarningsAsErrors",
        "OutputType",
        "ManagePackageVersionsCentrally",
        "CentralPackageVersionOverrideEnabled",
    ];

    public static IReadOnlyList<PropertyResult> Execute(
        Build build,
        string projectFilter,
        string? nameFilter = null)
    {
        var evaluation = ImportsQuery.FindEvaluation(build, projectFilter);
        if (evaluation == null)
            return [];

        // Get all properties from evaluation
        var allProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Walk the Properties folder under evaluation
        foreach (var child in evaluation.Children)
        {
            if (child is NamedNode named && named.Name == "Properties" && child is TreeNode folder)
            {
                foreach (var prop in folder.Children.OfType<Property>())
                {
                    allProperties[prop.Name] = prop.Value;
                }
            }

            // Also pick up direct Property children
            if (child is Property directProp)
            {
                allProperties[directProp.Name] = directProp.Value;
            }
        }

        // If nothing was found directly, try the GetProperties() approach
        // by walking all named nodes
        if (allProperties.Count == 0)
        {
            evaluation.VisitAllChildren<Property>(prop =>
            {
                allProperties[prop.Name] = prop.Value;
            });
        }

        IEnumerable<KeyValuePair<string, string>> filtered;

        if (!string.IsNullOrEmpty(nameFilter))
        {
            var filterNames = nameFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (filterNames.Length > 0)
            {
                var filterSet = new HashSet<string>(filterNames, StringComparer.OrdinalIgnoreCase);
                filtered = allProperties.Where(p => filterSet.Contains(p.Key));
            }
            else
            {
                // Substring match
                filtered = allProperties.Where(p =>
                    p.Key.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            // Return curated set
            filtered = allProperties.Where(p =>
                CuratedProperties.Contains(p.Key, StringComparer.OrdinalIgnoreCase));
        }

        return filtered
            .Select(p => new PropertyResult(p.Key, p.Value))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
