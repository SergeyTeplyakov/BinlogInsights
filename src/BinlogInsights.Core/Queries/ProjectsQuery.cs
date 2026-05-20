using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class ProjectsQuery
{
    public static IReadOnlyList<string> Execute(Build build)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        build.VisitAllChildren<Project>(project =>
        {
            if (!string.IsNullOrEmpty(project.ProjectFile))
                paths.Add(project.ProjectFile);
        });

        return paths.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Returns all .csproj files found in the binlog (from embedded source files,
    /// project evaluations, and project execution nodes), with a flag indicating
    /// whether each is a legacy (non-SDK) style project.
    /// </summary>
    public static IReadOnlyList<ProjectFileInfo> GetProjectFiles(Build build)
    {
        // Build a lookup of embedded source file content keyed by path
        var sourceContent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (build.SourceFiles != null)
        {
            foreach (var sf in build.SourceFiles)
            {
                if (sf.FullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                    sourceContent[sf.FullPath] = sf.Text;
            }
        }

        // Collect all csproj paths from multiple sources
        var csprojPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. From embedded source files
        foreach (var path in sourceContent.Keys)
            csprojPaths.Add(path);

        // 2. From Project execution nodes
        build.VisitAllChildren<Project>(project =>
        {
            if (!string.IsNullOrEmpty(project.ProjectFile) &&
                project.ProjectFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                csprojPaths.Add(project.ProjectFile);
        });

        // 3. From ProjectEvaluation nodes
        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            if (!string.IsNullOrEmpty(eval.ProjectFile) &&
                eval.ProjectFile.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                csprojPaths.Add(eval.ProjectFile);
        });

        // 4. From ProjectReference items in evaluations (resolving relative paths)
        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            var evalDir = !string.IsNullOrEmpty(eval.ProjectFile)
                ? Path.GetDirectoryName(eval.ProjectFile)
                : null;

            eval.VisitAllChildren<AddItem>(addItem =>
            {
                if (!string.Equals(addItem.Name, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (var child in addItem.Children.OfType<Item>())
                {
                    var itemPath = child.Text ?? child.Name ?? "";
                    if (!itemPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Resolve relative paths against the evaluation's project directory
                    if (!Path.IsPathRooted(itemPath) && evalDir != null)
                        itemPath = Path.GetFullPath(Path.Combine(evalDir, itemPath));

                    if (Path.IsPathRooted(itemPath))
                        csprojPaths.Add(itemPath);
                }
            });
        });

        // 5. From error/warning diagnostics referencing .csproj files
        build.VisitAllChildren<AbstractDiagnostic>(diagnostic =>
        {
            var file = diagnostic.File;
            if (!string.IsNullOrEmpty(file) &&
                file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) &&
                Path.IsPathRooted(file))
                csprojPaths.Add(file);
        });

        // Build a lookup of evaluations by project path so legacy detection
        // can read the UsingMicrosoftNETSdk property without rescanning the tree
        // for every project.
        var evaluationsByPath = new Dictionary<string, ProjectEvaluation>(StringComparer.OrdinalIgnoreCase);
        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            if (!string.IsNullOrEmpty(eval.ProjectFile) &&
                !evaluationsByPath.ContainsKey(eval.ProjectFile))
            {
                evaluationsByPath[eval.ProjectFile] = eval;
            }
        });

        // Build results with legacy detection
        var results = new List<ProjectFileInfo>();
        foreach (var path in csprojPaths)
        {
            bool isLegacy;
            if (evaluationsByPath.TryGetValue(path, out var eval))
            {
                // Primary: check the UsingMicrosoftNETSdk MSBuild property.
                // SDK-style projects set this to "true"; legacy projects do not.
                isLegacy = IsLegacyFromEvaluation(eval);
            }
            else if (sourceContent.TryGetValue(path, out var content))
            {
                // Fallback: project was referenced but never evaluated
                // (e.g. build failed before evaluation). Inspect embedded XML.
                isLegacy = IsLegacyProjectContent(content);
            }
            else
            {
                // No evaluation and no embedded content — can't determine.
                isLegacy = false;
            }

            results.Add(new ProjectFileInfo(path, isLegacy));
        }

        return results
            .OrderBy(p => p.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Detects whether a project file is legacy (non-SDK) style from its XML content.
    /// </summary>
    internal static bool IsLegacyProjectContent(string projectContent)
    {
        if (string.IsNullOrWhiteSpace(projectContent))
            return false;

        return projectContent.Contains(
            "<Project ToolsVersion=\"Current\" DefaultTargets=\"Build\"",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Detects whether a project is legacy (non-SDK) style by inspecting the
    /// UsingMicrosoftNETSdk MSBuild property in its evaluation. SDK-style
    /// projects (Microsoft.NET.Sdk and derivatives such as Microsoft.Build.NoTargets,
    /// Microsoft.Build.Traversal) set this property to "true" during evaluation.
    /// Legacy projects do not set it at all.
    /// </summary>
    internal static bool IsLegacyFromEvaluation(ProjectEvaluation evaluation)
    {
        var value = GetEvaluationPropertyValue(evaluation, "UsingMicrosoftNETSdk");
        return !string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads a property value from the "Properties" folder of a ProjectEvaluation.
    /// The Properties folder may contain Property nodes directly or grouped under
    /// sub-folders (e.g. "Global"); both layouts are searched.
    /// </summary>
    private static string? GetEvaluationPropertyValue(ProjectEvaluation evaluation, string propertyName)
    {
        var propertiesFolder = evaluation.Children
            .OfType<Folder>()
            .FirstOrDefault(f => f.Name == "Properties");

        if (propertiesFolder == null)
            return null;

        return FindPropertyValue(propertiesFolder, propertyName);
    }

    private static string? FindPropertyValue(TreeNode node, string propertyName)
    {
        foreach (var child in node.Children)
        {
            if (child is Property prop &&
                string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return prop.Value;
            }

            if (child is Folder folder)
            {
                var nested = FindPropertyValue(folder, propertyName);
                if (nested != null)
                    return nested;
            }
        }

        return null;
    }
}
