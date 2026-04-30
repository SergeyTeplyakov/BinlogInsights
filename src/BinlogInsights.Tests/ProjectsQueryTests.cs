using BinlogInsights.Core;
using BinlogInsights.Core.Queries;
using Xunit;

namespace BinlogInsights.Tests;

public class ProjectsQueryTests
{
    private static string GetBinlogFullPath()
    {
        // Navigate from bin/Debug/net8.0 up to the repo root (5 levels)
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var fullPath = Path.Combine(repoRoot, "samples", "binlogs", "failed_build_with_legacy_projects.binlog");
        return fullPath;
    }

    [Fact]
    public void GetProjectFiles_FindsAllCsprojFiles()
    {
        var fullPath = GetBinlogFullPath();
        Assert.True(File.Exists(fullPath), $"Binlog not found at: {fullPath}");

        var build = BinlogAnalyzer.LoadBuild(fullPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        Assert.NotEmpty(projects);

        // All entries should be .csproj files
        Assert.All(projects, p =>
            Assert.EndsWith(".csproj", p.FullPath, StringComparison.OrdinalIgnoreCase));

        // Should find csprojs from ProjectReference items (2) and error diagnostics (5)
        Assert.True(projects.Count >= 2,
            $"Expected at least 2 csproj files but found {projects.Count}: {string.Join(", ", projects.Select(p => Path.GetFileName(p.FullPath)))}");
    }

    [Fact]
    public void GetProjectFiles_FindsCsprojsFromProjectReferences()
    {
        var fullPath = GetBinlogFullPath();
        Assert.True(File.Exists(fullPath), $"Binlog not found at: {fullPath}");

        var build = BinlogAnalyzer.LoadBuild(fullPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        var fileNames = projects.Select(p => Path.GetFileName(p.FullPath)).ToList();

        // dirs.proj has ProjectReference items that include these csprojs
        Assert.Contains("AzureDataUploader.csproj", fileNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("AzureDataUploaderUnitTests.csproj", fileNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProjectFiles_FindsCsprojsFromErrorDiagnostics()
    {
        var fullPath = GetBinlogFullPath();
        Assert.True(File.Exists(fullPath), $"Binlog not found at: {fullPath}");

        var build = BinlogAnalyzer.LoadBuild(fullPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        var fileNames = projects.Select(p => Path.GetFileName(p.FullPath)).ToList();

        // Error messages reference csproj files that couldn't be loaded
        Assert.Contains("CommonUtil.csproj", fileNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Microsoft.Search.Autopilot.ManagedUtils.csproj", fileNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetProjectFiles_ReturnsFullPaths()
    {
        var fullPath = GetBinlogFullPath();
        Assert.True(File.Exists(fullPath), $"Binlog not found at: {fullPath}");

        var build = BinlogAnalyzer.LoadBuild(fullPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        // All paths should be rooted (full paths)
        Assert.All(projects, p => Assert.True(
            Path.IsPathRooted(p.FullPath),
            $"Expected rooted path but got: {p.FullPath}"));
    }

    [Fact]
    public void GetProjectFiles_LegacyDetection_RequiresContentOrEvaluation()
    {
        // In this binlog, the build failed during restore so no csproj files
        // were evaluated or embedded. Legacy detection defaults to false.
        var fullPath = GetBinlogFullPath();
        Assert.True(File.Exists(fullPath), $"Binlog not found at: {fullPath}");

        var build = BinlogAnalyzer.LoadBuild(fullPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        // Without content or evaluations, all projects should have IsLegacy=false
        // (we can't determine style without data)
        Assert.All(projects, p => Assert.False(p.IsLegacy,
            $"Expected IsLegacy=false for {p.FullPath} since no content/evaluation is available"));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup></PropertyGroup></Project>", false)]
    [InlineData("<Project><Sdk Name=\"Microsoft.NET.Sdk\" /><PropertyGroup></PropertyGroup></Project>", false)]
    [InlineData("<Project ToolsVersion=\"Current\"><PropertyGroup></PropertyGroup></Project>", false)]
    [InlineData("<Project ToolsVersion=\"14.0\" DefaultTargets=\"Build\"><PropertyGroup></PropertyGroup></Project>", false)]
    [InlineData("<Project ToolsVersion=\"Current\" DefaultTargets=\"Build\"><PropertyGroup></PropertyGroup></Project>", true)]
    [InlineData("<Project ToolsVersion=\"Current\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\"></Project>", true)]
    public void IsLegacyProjectContent_DetectsCorrectly(string content, bool expectedLegacy)
    {
        var result = ProjectsQuery.IsLegacyProjectContent(content);
        Assert.Equal(expectedLegacy, result);
    }
}
