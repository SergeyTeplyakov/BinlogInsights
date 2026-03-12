using System.Diagnostics;
using BinlogInsights.Core;
using BinlogInsights.Core.Queries;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        if (command is "--help" or "-h" or "help")
        {
            PrintUsage();
            return 0;
        }

        // Compare command needs two binlog paths
        if (command == "compare")
        {
            return RunCompare(args[1..]);
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: binlog path is required.");
            Console.Error.WriteLine("Usage: binlog-insights <command> <binlog-path> [options]");
            return 1;
        }

        var binlogPath = args[1];

        try
        {
            var sw = Stopwatch.StartNew();
            var build = BinlogAnalyzer.LoadBuild(binlogPath);
            sw.Stop();
            Console.Error.WriteLine($"[Loaded binlog in {sw.Elapsed.TotalSeconds:F1}s]");

            return command switch
            {
                "overview" => RunOverview(build),
                "errors" => RunErrors(build, args[2..]),
                "warnings" => RunWarnings(build, args[2..]),
                "imports" => RunImports(build, args[2..]),
                "properties" => RunProperties(build, args[2..]),
                "items" => RunItems(build, args[2..]),
                "nuget" => RunNuGet(build, args[2..]),
                "preprocess" => RunPreprocess(build, args[2..]),
                "compiler" => RunCompiler(build, args[2..]),
                "projects" => RunProjects(build),
                "search" => RunSearch(build, args[2..]),
                _ => UnknownCommand(command),
            };
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int RunOverview(Build build)
    {
        var result = BuildOverviewQuery.Execute(build);
        Formatters.OverviewFormatter.Print(result);
        return result.Succeeded ? 0 : 1;
    }

    private static int RunErrors(Build build, string[] opts)
    {
        ParseCommonOptions(opts, out var projectFilter, out var limit, out var offset);
        var errors = DiagnosticsQuery.GetErrors(build, projectFilter, limit, offset);
        var totalCount = DiagnosticsQuery.CountErrors(build);
        Formatters.DiagnosticsFormatter.Print(errors, "error", totalCount, offset, limit);
        return errors.Count == 0 ? 2 : 0;
    }

    private static int RunWarnings(Build build, string[] opts)
    {
        ParseCommonOptions(opts, out var projectFilter, out var limit, out var offset);
        var code = GetOption(opts, "--code");
        var warnings = DiagnosticsQuery.GetWarnings(build, projectFilter, code, limit, offset);
        var totalCount = DiagnosticsQuery.CountWarnings(build);
        Formatters.DiagnosticsFormatter.Print(warnings, "warning", totalCount, offset, limit);
        return warnings.Count == 0 ? 2 : 0;
    }

    private static int RunImports(Build build, string[] opts)
    {
        var projectFilter = RequireProjectFilter(opts);
        if (projectFilter == null) return 1;
        var imports = ImportsQuery.Execute(build, projectFilter);
        Formatters.ImportsFormatter.Print(imports);
        return imports.Count == 0 ? 2 : 0;
    }

    private static int RunProperties(Build build, string[] opts)
    {
        var projectFilter = RequireProjectFilter(opts);
        if (projectFilter == null) return 1;
        var nameFilter = GetOption(opts, "--filter");
        var properties = PropertiesQuery.Execute(build, projectFilter, nameFilter);
        Formatters.PropertiesFormatter.Print(properties);
        return properties.Count == 0 ? 2 : 0;
    }

    private static int RunItems(Build build, string[] opts)
    {
        var projectFilter = RequireProjectFilter(opts);
        if (projectFilter == null) return 1;
        var itemType = GetOption(opts, "--type");
        if (string.IsNullOrEmpty(itemType))
        {
            // List available item types
            var types = ItemsQuery.GetItemTypes(build, projectFilter);
            Console.WriteLine("Available item types:");
            foreach (var t in types)
                Console.WriteLine($"  {t}");
            return 0;
        }
        ParseCommonOptions(opts, out _, out var limit, out var offset);
        var items = ItemsQuery.Execute(build, projectFilter, itemType, limit, offset);
        Formatters.ItemsFormatter.Print(items, itemType);
        return items.Count == 0 ? 2 : 0;
    }

    private static int RunNuGet(Build build, string[] opts)
    {
        ParseCommonOptions(opts, out var projectFilter, out _, out _);
        var result = NuGetRestoreQuery.Execute(build, projectFilter);
        Formatters.NuGetFormatter.Print(result);
        return result.RestoreSucceeded ? 0 : 1;
    }

    private static int RunPreprocess(Build build, string[] opts)
    {
        var projectFilter = RequireProjectFilter(opts);
        if (projectFilter == null) return 1;
        var maxLengthStr = GetOption(opts, "--max-length");
        var maxLength = int.TryParse(maxLengthStr, out var ml) ? ml : 30_000;
        var result = PreprocessQuery.Execute(build, projectFilter, maxLength);
        if (result == null)
        {
            Console.Error.WriteLine("No matching project evaluation found.");
            return 2;
        }
        Console.WriteLine(result);
        return 0;
    }

    private static int RunCompiler(Build build, string[] opts)
    {
        ParseCommonOptions(opts, out var projectFilter, out _, out _);
        var invocations = CompilerInvocationQuery.Execute(build, projectFilter);
        Formatters.CompilerFormatter.Print(invocations);
        return invocations.Count == 0 ? 2 : 0;
    }

    private static int RunProjects(Build build)
    {
        var projects = ProjectsQuery.Execute(build);
        Formatters.ProjectsFormatter.Print(projects);
        return projects.Count == 0 ? 2 : 0;
    }

    private static int RunSearch(Build build, string[] opts)
    {
        var query = GetOption(opts, "--query");
        if (string.IsNullOrEmpty(query))
        {
            Console.Error.WriteLine("Error: --query is required for search.");
            return 1;
        }
        ParseCommonOptions(opts, out var projectFilter, out var limit, out var offset);
        var results = SearchQuery.Execute(build, query, projectFilter, limit, offset);
        Formatters.SearchFormatter.Print(results, query);
        return results.Count == 0 ? 2 : 0;
    }

    private static int RunCompare(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: compare requires two binlog paths.");
            Console.Error.WriteLine("Usage: binlog-insights compare <binlogA> <binlogB> [--project <filter>]");
            return 1;
        }

        var pathA = args[0];
        var pathB = args[1];
        var opts = args[2..];
        var projectFilter = GetOption(opts, "--project");

        try
        {
            var sw = Stopwatch.StartNew();
            var buildA = BinlogAnalyzer.LoadBuild(pathA);
            Console.Error.WriteLine($"[Loaded A in {sw.Elapsed.TotalSeconds:F1}s]");
            sw.Restart();
            var buildB = BinlogAnalyzer.LoadBuild(pathB);
            Console.Error.WriteLine($"[Loaded B in {sw.Elapsed.TotalSeconds:F1}s]");

            var result = CompareQuery.Execute(buildA, buildB, projectFilter);
            var labelA = Path.GetFileNameWithoutExtension(pathA);
            var labelB = Path.GetFileNameWithoutExtension(pathB);
            Formatters.CompareFormatter.Print(result, labelA, labelB);
            return 0;
        }
        catch (FileNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
    }

    // --- Option parsing helpers ---

    private static void ParseCommonOptions(string[] opts, out string? projectFilter, out int limit, out int offset)
    {
        projectFilter = GetOption(opts, "--project");
        limit = int.TryParse(GetOption(opts, "--limit"), out var l) ? l : 50;
        offset = int.TryParse(GetOption(opts, "--offset"), out var o) ? o : 0;
    }

    private static string? RequireProjectFilter(string[] opts)
    {
        var filter = GetOption(opts, "--project");
        if (string.IsNullOrEmpty(filter))
        {
            Console.Error.WriteLine("Error: --project <filter> is required for this command.");
            return null;
        }
        return filter;
    }

    private static string? GetOption(string[] opts, string name)
    {
        for (int i = 0; i < opts.Length - 1; i++)
        {
            if (string.Equals(opts[i], name, StringComparison.OrdinalIgnoreCase))
                return opts[i + 1];
        }
        return null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            BinlogInsights - MSBuild binary log analysis tool

            Usage: binlog-insights <command> <binlog-path> [options]

            Commands:
              overview    Build overview (success, duration, projects, error/warning counts)
              errors      List build errors with project/target/task context
              warnings    List build warnings with context
              imports     Show project import tree (Directory.Build.props, SDK imports, etc.)
              properties  Show evaluated MSBuild properties for a project
              items       Show MSBuild items (Compile, PackageReference, etc.) for a project
              nuget       NuGet restore diagnostics (packages, restore status, errors)
              preprocess  Show effective project XML with embedded source files
              compiler    Show compiler (csc/vbc) command-line invocations
              projects    List all project files involved in the build
              search      Search all build messages by text
              compare     Compare two binlogs side-by-side (properties, imports, references)

            Common options:
              --project <filter>  Filter by project path (substring match)
              --limit <n>         Max results (default: 50)
              --offset <n>        Skip first n results (default: 0)

            Command-specific options:
              warnings:   --code <code>          Filter by warning code (e.g. CS0618)
              properties: --filter <name1,name2>  Filter by property names
              items:      --type <itemType>       Item type (e.g. Compile, PackageReference)
              preprocess: --max-length <n>        Max output chars (default: 30000)
              search:     --query <text>          Search text (required)
              compare:    <binlogA> <binlogB>    Two binlog files to compare

            Examples:
              binlog-insights overview msbuild.binlog
              binlog-insights errors msbuild.binlog --project MyApp
              binlog-insights imports msbuild.binlog --project MyApp.csproj
              binlog-insights properties msbuild.binlog --project MyApp --filter TargetFramework,OutputPath
              binlog-insights items msbuild.binlog --project MyApp --type PackageReference
              binlog-insights search msbuild.binlog --query "could not be found"
              binlog-insights compare build1.binlog build2.binlog --project MyApp
            """);
    }
}
