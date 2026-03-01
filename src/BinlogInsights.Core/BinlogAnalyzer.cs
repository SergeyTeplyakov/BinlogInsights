using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core;

/// <summary>
/// Entry point for loading and analyzing MSBuild binary log files.
/// </summary>
public static class BinlogAnalyzer
{
    /// <summary>
    /// Reads and analyzes a binlog file, returning the fully-populated Build tree.
    /// </summary>
    public static Build LoadBuild(string binlogPath)
    {
        if (!File.Exists(binlogPath))
            throw new FileNotFoundException($"Binlog file not found: {binlogPath}");

        var build = BinaryLog.ReadBuild(binlogPath);
        BuildAnalyzer.AnalyzeBuild(build);
        return build;
    }
}
