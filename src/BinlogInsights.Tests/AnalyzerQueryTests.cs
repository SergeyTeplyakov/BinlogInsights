using BinlogInsights.Core;
using BinlogInsights.Core.Queries;
using Xunit;

namespace BinlogInsights.Tests;

public class AnalyzerQueryTests
{
    private static string GetBinlogFullPath(string fileName)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var fullPath = Path.Combine(repoRoot, "samples", "binlogs", fileName);
        Assert.True(File.Exists(fullPath), $"Binlog not found: {fullPath}");
        return fullPath;
    }

    [Fact]
    public void GetExpensiveAnalyzers_ReturnsResults_ForBinlogWithAnalyzerData()
    {
        // reportanalyzer.binlog was built with /p:ReportAnalyzer=true
        var build = BinlogAnalyzer.LoadBuild(GetBinlogFullPath("reportanalyzer.binlog"));
        var results = AnalyzerQuery.GetExpensiveAnalyzers(build);

        // This should not be empty if the binlog has ReportAnalyzer data
        Assert.NotEmpty(results);
    }

    [Fact]
    public void GetExpensiveAnalyzers_ReturnsEmpty_ForBinlogWithoutAnalyzerData()
    {
        var build = BinlogAnalyzer.LoadBuild(GetBinlogFullPath("msbuild_Works.binlog"));
        var results = AnalyzerQuery.GetExpensiveAnalyzers(build);

        Assert.Empty(results);
    }
}
