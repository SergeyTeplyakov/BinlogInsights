using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class PreprocessQuery
{
    /// <summary>
    /// Produces a preprocessed view of the project by inlining all imported files
    /// from the embedded source archive in the binlog.
    /// </summary>
    public static string? Execute(Build build, string projectFilter, int maxLength = 30_000)
    {
        var evaluation = ImportsQuery.FindEvaluation(build, projectFilter);
        if (evaluation == null)
            return null;

        if (build.SourceFiles == null || build.SourceFiles.Count == 0)
            return "[No embedded source files in binlog. Rebuild with 'dotnet build /bl' or 'msbuild /bl' to embed project files.]";

        try
        {
            // Build a lookup from path to content using the embedded source files
            var sourceFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sf in build.SourceFiles)
            {
                sourceFileMap[sf.FullPath] = sf.Text;
            }

            var projectFile = evaluation.ProjectFile;
            if (projectFile == null || !sourceFileMap.ContainsKey(projectFile))
                return "[Project file not found in embedded source files]";

            // Build the preprocessed output by walking imports
            var sb = new StringBuilder();
            sb.AppendLine($"<!-- Preprocessed: {projectFile} -->");
            sb.AppendLine();

            // Start with the main project file content
            var mainContent = sourceFileMap[projectFile];
            sb.AppendLine(mainContent);

            // Append a summary of all imports
            sb.AppendLine();
            sb.AppendLine("<!-- === Import chain === -->");
            var imports = ImportsQuery.Execute(build, projectFilter);
            AppendImportSummary(sb, imports, sourceFileMap, indent: 0);

            var result = sb.ToString();
            if (result.Length > maxLength)
            {
                return result[..maxLength] +
                    $"\n\n[... truncated at {maxLength} chars. Full length: {result.Length}. Use --max-length to increase.]";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"[Preprocessing failed: {ex.Message}]";
        }
    }

    private static void AppendImportSummary(
        StringBuilder sb,
        IReadOnlyList<Models.ImportResult> imports,
        Dictionary<string, string> sourceFileMap,
        int indent)
    {
        var prefix = new string(' ', indent * 2);
        foreach (var import in imports)
        {
            var marker = import.IsMissing ? "[MISSING] " : "";
            var hasContent = !import.IsMissing && sourceFileMap.ContainsKey(import.ImportedFile);
            sb.AppendLine($"{prefix}<!-- {marker}Import: {import.ImportedFile} (from {import.ImportedBy ?? "?"} line {import.Line}) -->");

            if (import.Children.Count > 0)
                AppendImportSummary(sb, import.Children, sourceFileMap, indent + 1);
        }
    }
}
