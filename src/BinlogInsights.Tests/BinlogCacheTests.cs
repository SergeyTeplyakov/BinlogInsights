using BinlogInsights.Mcp;
using Xunit;

namespace BinlogInsights.Tests;

public class BinlogCacheTests
{
    private static string GetSampleBinlog()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var path = Path.Combine(repoRoot, "samples", "binlogs", "msbuild_Works.binlog");
        Assert.True(File.Exists(path), $"Sample binlog not found: {path}");
        return path;
    }

    private static string CopyToTemp(string sourcePath)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"binlog_test_{Guid.NewGuid():N}.binlog");
        File.Copy(sourcePath, tempFile);
        return tempFile;
    }

    [Fact]
    public void Load_ValidFile_ReturnsBuild()
    {
        var cache = new BinlogCache();
        var build = cache.Load(GetSampleBinlog());
        Assert.NotNull(build);
    }

    [Fact]
    public void Load_SecondCall_ReturnsCachedBuild()
    {
        var cache = new BinlogCache();
        var source = GetSampleBinlog();
        var build1 = cache.Load(source);
        var build2 = cache.Load(source);
        Assert.Same(build1, build2);
    }

    [Fact]
    public void Load_FileNeverExisted_ThrowsWithClearMessage()
    {
        var cache = new BinlogCache();
        var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N") + ".binlog");

        var ex = Assert.Throws<BinlogAnalysisException>(() => cache.Load(fakePath));
        Assert.Contains("Binlog file not found", ex.Message);
        Assert.Contains("dotnet build /bl", ex.RecommendedAction);
        Assert.Equal(Path.GetFullPath(fakePath), ex.BinlogPath);
    }

    [Fact]
    public void Load_FileDeletedBeforeFirstLoad_ThrowsWithClearMessage()
    {
        var cache = new BinlogCache();
        var tempFile = CopyToTemp(GetSampleBinlog());
        File.Delete(tempFile);

        var ex = Assert.Throws<BinlogAnalysisException>(() => cache.Load(tempFile));
        Assert.Contains("Binlog file not found", ex.Message);
        Assert.Contains("dotnet build /bl", ex.RecommendedAction);
    }

    [Fact]
    public void Load_FileDeletedAfterCaching_InvalidatesCacheAndThrows()
    {
        var cache = new BinlogCache();
        var tempFile = CopyToTemp(GetSampleBinlog());

        try
        {
            // Load once to populate cache
            var build = cache.Load(tempFile);
            Assert.NotNull(build);

            // Delete the file
            File.Delete(tempFile);

            // Should throw with a clear message, not return stale data
            var ex = Assert.Throws<BinlogAnalysisException>(() => cache.Load(tempFile));
            Assert.Equal(Path.GetFullPath(tempFile), ex.BinlogPath);
            Assert.Contains("dotnet build /bl", ex.RecommendedAction);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_RelativePathNotFound_ThrowsWithClearMessage()
    {
        var cache = new BinlogCache();
        var relativePath = $"nonexistent_{Guid.NewGuid():N}.binlog";

        var ex = Assert.Throws<BinlogAnalysisException>(() => cache.Load(relativePath));
        Assert.Contains(relativePath, ex.Message);
        Assert.Contains("resolved to", ex.Message);
        Assert.Contains("absolute path", ex.RecommendedAction);
        Assert.Contains("Binlog Analyzer", ex.RecommendedAction);
    }

    [Fact]
    public void Load_FileDeletedThenRecreated_LoadsFreshBuild()
    {
        var cache = new BinlogCache();
        var tempFile = CopyToTemp(GetSampleBinlog());

        try
        {
            // Load, delete, then recreate
            var build1 = cache.Load(tempFile);
            File.Delete(tempFile);

            Assert.Throws<BinlogAnalysisException>(() => cache.Load(tempFile));

            // Recreate the file
            File.Copy(GetSampleBinlog(), tempFile);
            var build2 = cache.Load(tempFile);

            Assert.NotNull(build2);
            // Should be a fresh load, not the old cached instance
            Assert.NotSame(build1, build2);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
