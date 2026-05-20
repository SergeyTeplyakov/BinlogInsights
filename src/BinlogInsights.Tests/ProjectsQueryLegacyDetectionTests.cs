using System.Diagnostics;
using BinlogInsights.Core;
using BinlogInsights.Core.Queries;
using Xunit;

namespace BinlogInsights.Tests;

/// <summary>
/// Integration tests that generate two minimal projects (one SDK-style, one
/// legacy non-SDK style), build them to produce real binlogs, and verify that
/// <see cref="ProjectsQuery.GetProjectFiles"/> classifies each project
/// correctly using the <c>UsingMicrosoftNETSdk</c> evaluation property.
/// </summary>
public class ProjectsQueryLegacyDetectionTests : IClassFixture<ProjectsQueryLegacyDetectionTests.BuildFixture>
{
    private readonly BuildFixture _fixture;

    public ProjectsQueryLegacyDetectionTests(BuildFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void SdkProject_IsClassifiedAsNotLegacy()
    {
        var build = BinlogAnalyzer.LoadBuild(_fixture.SdkBinlogPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        var sdkProject = projects.SingleOrDefault(p =>
            string.Equals(Path.GetFileName(p.FullPath), "SdkProj.csproj", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(sdkProject);
        Assert.False(sdkProject!.IsLegacy,
            $"Expected SdkProj.csproj to be SDK-style (IsLegacy=false) but was IsLegacy=true. Binlog: {_fixture.SdkBinlogPath}");
    }

    [Fact]
    public void LegacyProject_IsClassifiedAsLegacy()
    {
        var build = BinlogAnalyzer.LoadBuild(_fixture.LegacyBinlogPath);
        var projects = ProjectsQuery.GetProjectFiles(build);

        var legacyProject = projects.SingleOrDefault(p =>
            string.Equals(Path.GetFileName(p.FullPath), "LegacyProj.csproj", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(legacyProject);
        Assert.True(legacyProject!.IsLegacy,
            $"Expected LegacyProj.csproj to be legacy (IsLegacy=true) but was IsLegacy=false. Binlog: {_fixture.LegacyBinlogPath}");
    }

    /// <summary>
    /// xUnit class fixture that creates two temporary projects on disk and
    /// invokes MSBuild on each to produce binary logs. The fixture is shared
    /// across all tests in this class to avoid rebuilding for every test.
    /// </summary>
    public sealed class BuildFixture : IDisposable
    {
        private readonly string _rootDir;

        public string SdkBinlogPath { get; }
        public string LegacyBinlogPath { get; }

        public BuildFixture()
        {
            _rootDir = Path.Combine(Path.GetTempPath(), $"binlog_legacy_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_rootDir);

            SdkBinlogPath = BuildSdkProject();
            LegacyBinlogPath = BuildLegacyProject();
        }

        private string BuildSdkProject()
        {
            var dir = Path.Combine(_rootDir, "SdkProj");
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "SdkProj.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <OutputType>Library</OutputType>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(dir, "Class1.cs"),
                """
                namespace SdkProj;
                public class Class1 { }
                """);

            var binlog = Path.Combine(dir, "SdkProj.binlog");
            RunDotnet(dir, $"build /bl:\"{binlog}\" -nologo", expectSuccess: true);
            return binlog;
        }

        private string BuildLegacyProject()
        {
            var dir = Path.Combine(_rootDir, "LegacyProj");
            Directory.CreateDirectory(dir);

            File.WriteAllText(Path.Combine(dir, "LegacyProj.csproj"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <Project ToolsVersion="Current" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
                    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
                    <OutputType>Library</OutputType>
                    <RootNamespace>LegacyProj</RootNamespace>
                    <AssemblyName>LegacyProj</AssemblyName>
                    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
                    <FileAlignment>512</FileAlignment>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="Class1.cs" />
                  </ItemGroup>
                  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
                </Project>
                """);

            File.WriteAllText(Path.Combine(dir, "Class1.cs"),
                """
                namespace LegacyProj;
                public class Class1 { }
                """);

            var binlog = Path.Combine(dir, "LegacyProj.binlog");
            // Use `dotnet msbuild` (not `dotnet build`) because `dotnet build`
            // requires NuGet restore which is not meaningful for a legacy
            // project. The build itself is expected to fail (no output path,
            // no framework reference assemblies on the build agent) — we only
            // need the evaluation to be captured in the binlog.
            RunDotnet(dir, $"msbuild /bl:\"{binlog}\" -nologo", expectSuccess: false);
            return binlog;
        }

        private static void RunDotnet(string workingDir, string args, bool expectSuccess)
        {
            var psi = new ProcessStartInfo("dotnet", args)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start dotnet process");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (expectSuccess && process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"dotnet {args} failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                    $"stdout:{Environment.NewLine}{stdout}{Environment.NewLine}" +
                    $"stderr:{Environment.NewLine}{stderr}");
            }
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_rootDir))
                    Directory.Delete(_rootDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; ignore failures (locked files, etc.).
            }
        }
    }
}
