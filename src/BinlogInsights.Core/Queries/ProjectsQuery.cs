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

        // Build results with legacy detection
        var results = new List<ProjectFileInfo>();
        foreach (var path in csprojPaths)
        {
            bool isLegacy;
            if (sourceContent.TryGetValue(path, out var content))
            {
                // We have the actual file content — check it directly
                isLegacy = IsLegacyProjectContent(content);
            }
            else
            {
                // No embedded content — try to detect from evaluation imports
                isLegacy = IsLegacyFromEvaluation(build, path);
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
    /// Heuristic: detect legacy projects from their evaluation import tree.
    /// Legacy projects import Microsoft.CSharp.targets directly rather than through SDK resolution.
    /// SDK-style projects import via Sdk.props/Sdk.targets from an SDK path.
    /// </summary>
    internal static bool IsLegacyFromEvaluation(Build build, string projectPath)
    {
        ProjectEvaluation? evaluation = null;
        build.VisitAllChildren<ProjectEvaluation>(eval =>
        {
            if (evaluation != null) return;
            if (string.Equals(eval.ProjectFile, projectPath, StringComparison.OrdinalIgnoreCase))
                evaluation = eval;
        });

        if (evaluation == null)
            return false;

        // Walk the import tree looking for SDK-style markers
        bool hasSdkImport = false;
        bool hasLegacyCSharpTargets = false;

        evaluation.VisitAllChildren<Import>(import =>
        {
            var importedFile = import.ImportedProjectFilePath ?? import.Text ?? "";

            // SDK-style: imports come from Sdk directories
            if (importedFile.Contains("Sdk.props", StringComparison.OrdinalIgnoreCase) ||
                importedFile.Contains("Sdk.targets", StringComparison.OrdinalIgnoreCase))
            {
                // But only if it's a real SDK (like Microsoft.NET.Sdk), not just any file with Sdk in the name
                if (importedFile.Contains("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) ||
                    importedFile.Contains("Microsoft.Build.NoTargets", StringComparison.OrdinalIgnoreCase) ||
                    importedFile.Contains("Microsoft.Build.Traversal", StringComparison.OrdinalIgnoreCase))
                    hasSdkImport = true;
            }

            // Legacy marker: direct import of Microsoft.CSharp.targets (not through SDK)
            if (importedFile.EndsWith("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase))
                hasLegacyCSharpTargets = true;
        });

        // If we found SDK imports, it's SDK-style
        if (hasSdkImport)
            return false;

        // If we found legacy CSharp targets without SDK, it's legacy
        if (hasLegacyCSharpTargets)
            return true;

        // Default: can't determine, assume not legacy
        return false;
    }
}
