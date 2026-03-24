namespace BinlogInsights.Mcp;

/// <summary>
/// Represents an error during binlog analysis that should be returned
/// as a graceful MCP tool error with an actionable message.
/// </summary>
public class BinlogAnalysisException : Exception
{
    public string BinlogPath { get; }
    public string RecommendedAction { get; }

    public BinlogAnalysisException(string message, string binlogPath, string recommendedAction, Exception? innerException = null)
        : base(message, innerException)
    {
        BinlogPath = binlogPath;
        RecommendedAction = recommendedAction;
    }

    public static BinlogAnalysisException FileNotFound(string binlogPath) =>
        new($"Binlog file not found: '{binlogPath}'.",
            binlogPath,
            "Please verify the path or rebuild with 'dotnet build /bl' to generate a new binlog.");

    public static BinlogAnalysisException RelativePathNotFound(string relativePath, string resolvedPath) =>
        new($"Binlog file not found: '{relativePath}' (resolved to '{resolvedPath}').",
            resolvedPath,
            $"The relative path '{relativePath}' was resolved against the server's working directory. " +
            "Either use an absolute path " +
            "or install the Binlog Analyzer VS Code extension which automatically sets the working directory to your workspace.");

    public static BinlogAnalysisException FileDeleted(string binlogPath) =>
        new($"Binlog file was deleted or moved: '{binlogPath}'.",
            binlogPath,
            "The file may have been renamed, moved, or deleted. Please provide the new path or rebuild with 'dotnet build /bl'.");

    public static BinlogAnalysisException IoError(string binlogPath, IOException ex) =>
        new($"Failed to read binlog file: '{binlogPath}'. {ex.Message}",
            binlogPath,
            "The file may be locked, corrupted, or on an unavailable network share. Please ensure the file is accessible and try again.",
            ex);

    public static BinlogAnalysisException AccessDenied(string binlogPath, UnauthorizedAccessException ex) =>
        new($"Access denied reading binlog file: '{binlogPath}'.",
            binlogPath,
            "You don't have permission to read this file. Check file permissions or run with appropriate access.",
            ex);
}
