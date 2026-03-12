using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class FileQuery
{
    /// <summary>
    /// Lists all embedded source files in the binlog, optionally filtered by path substring.
    /// </summary>
    public static IReadOnlyList<string> ListFiles(
        Build build,
        string? pathFilter = null,
        int limit = 100,
        int offset = 0)
    {
        var files = build.SourceFiles?.Select(f => f.FullPath) ?? [];

        if (!string.IsNullOrEmpty(pathFilter))
        {
            files = files.Where(f => f.Contains(pathFilter, StringComparison.OrdinalIgnoreCase));
        }

        return files
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Gets the content of a specific embedded source file from the binlog.
    /// </summary>
    public static string? GetFileContent(Build build, string filePath)
    {
        return build.SourceFiles?
            .FirstOrDefault(f => string.Equals(f.FullPath, filePath, StringComparison.OrdinalIgnoreCase))?
            .Text;
    }
}
