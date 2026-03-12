using System.Text.RegularExpressions;
using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class CompareQuery
{
    public static CompareResult Execute(
        Build buildA,
        Build buildB,
        string? projectFilter = null)
    {
        var overviewA = BuildOverviewQuery.Execute(buildA);
        var overviewB = BuildOverviewQuery.Execute(buildB);

        // Find matching projects to compare
        var projFilter = projectFilter ?? FindCommonProject(overviewA, overviewB);

        var propertyDiffs = CompareProperties(buildA, buildB, projFilter);
        var (importsOnlyA, importsOnlyB) = CompareImports(buildA, buildB, projFilter);
        var (refsOnlyA, refsOnlyB) = CompareReferences(buildA, buildB, projFilter);
        var packageDiffs = ComparePackages(buildA, buildB, projFilter);
        var solutionPackageDiffs = CompareAllPackages(buildA, buildB);

        return new CompareResult(
            overviewA, overviewB,
            propertyDiffs,
            importsOnlyA, importsOnlyB,
            refsOnlyA, refsOnlyB,
            packageDiffs,
            solutionPackageDiffs);
    }

    private static string? FindCommonProject(BuildOverviewResult a, BuildOverviewResult b)
    {
        // Find the first project name that appears in both builds
        foreach (var pa in a.Projects)
        {
            var nameA = Path.GetFileNameWithoutExtension(pa.ProjectFile);
            foreach (var pb in b.Projects)
            {
                var nameB = Path.GetFileNameWithoutExtension(pb.ProjectFile);
                if (string.Equals(nameA, nameB, StringComparison.OrdinalIgnoreCase))
                    return nameA;
            }
        }
        return null;
    }

    private static IReadOnlyList<PropertyDiff> CompareProperties(
        Build buildA, Build buildB, string? projectFilter)
    {
        if (projectFilter == null) return [];

        var propsA = PropertiesQuery.Execute(buildA, projectFilter)
            .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        var propsB = PropertiesQuery.Execute(buildB, projectFilter)
            .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

        var allKeys = new HashSet<string>(propsA.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(propsB.Keys);

        var diffs = new List<PropertyDiff>();
        foreach (var key in allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            propsA.TryGetValue(key, out var valA);
            propsB.TryGetValue(key, out var valB);

            if (!string.Equals(valA, valB, StringComparison.Ordinal))
            {
                diffs.Add(new PropertyDiff(key, valA, valB));
            }
        }

        return diffs;
    }

    private static (IReadOnlyList<string> onlyA, IReadOnlyList<string> onlyB) CompareImports(
        Build buildA, Build buildB, string? projectFilter)
    {
        if (projectFilter == null) return ([], []);

        var importsA = ImportsQuery.Execute(buildA, projectFilter);
        var importsB = ImportsQuery.Execute(buildB, projectFilter);

        var filesA = FlattenImportFiles(importsA);
        var filesB = FlattenImportFiles(importsB);

        // Compare by filename (paths will differ between environments)
        var namesA = filesA.Select(f => Path.GetFileName(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var namesB = filesB.Select(f => Path.GetFileName(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var onlyA = namesA.Except(namesB, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
        var onlyB = namesB.Except(namesA, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();

        return (onlyA, onlyB);
    }

    private static HashSet<string> FlattenImportFiles(IReadOnlyList<ImportResult> imports)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        FlattenImportFilesRecursive(imports, files);
        return files;
    }

    private static void FlattenImportFilesRecursive(IReadOnlyList<ImportResult> imports, HashSet<string> files)
    {
        foreach (var import in imports)
        {
            if (!import.IsMissing)
                files.Add(import.ImportedFile);
            FlattenImportFilesRecursive(import.Children, files);
        }
    }

    private static (IReadOnlyList<string> onlyA, IReadOnlyList<string> onlyB) CompareReferences(
        Build buildA, Build buildB, string? projectFilter)
    {
        var refsA = ExtractReferenceNames(buildA, projectFilter);
        var refsB = ExtractReferenceNames(buildB, projectFilter);

        var onlyA = refsA.Except(refsB, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
        var onlyB = refsB.Except(refsA, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();

        return (onlyA, onlyB);
    }

    private static IReadOnlyList<PackageDiff> ComparePackages(
        Build buildA, Build buildB, string? projectFilter)
    {
        if (projectFilter == null) return [];

        var pkgA = ExtractPackageVersions(buildA, projectFilter);
        var pkgB = ExtractPackageVersions(buildB, projectFilter);

        var allNames = new HashSet<string>(pkgA.Keys, StringComparer.OrdinalIgnoreCase);
        allNames.UnionWith(pkgB.Keys);

        var diffs = new List<PackageDiff>();
        foreach (var name in allNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            pkgA.TryGetValue(name, out var verA);
            pkgB.TryGetValue(name, out var verB);

            if (!string.Equals(verA, verB, StringComparison.OrdinalIgnoreCase))
            {
                diffs.Add(new PackageDiff(name, verA, verB));
            }
        }

        return diffs;
    }

    private static Dictionary<string, string> ExtractPackageVersions(Build build, string projectFilter)
    {
        var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // First try PackageVersion items (central package management)
        var packageVersionItems = ItemsQuery.Execute(build, projectFilter, "PackageVersion", limit: 10000);
        foreach (var item in packageVersionItems)
        {
            var version = item.Metadata.FirstOrDefault(m =>
                string.Equals(m.Name, "Version", StringComparison.OrdinalIgnoreCase));
            if (version != null)
                packages[item.Include] = version.Value;
        }

        // Also check PackageReference items for Version metadata (non-CPM projects)
        var packageRefItems = ItemsQuery.Execute(build, projectFilter, "PackageReference", limit: 10000);
        foreach (var item in packageRefItems)
        {
            if (packages.ContainsKey(item.Include))
                continue; // PackageVersion takes precedence

            var version = item.Metadata.FirstOrDefault(m =>
                string.Equals(m.Name, "Version", StringComparison.OrdinalIgnoreCase));
            if (version != null)
                packages[item.Include] = version.Value;
        }

        return packages;
    }

    private static IReadOnlyList<PackageDiff> CompareAllPackages(
        Build buildA, Build buildB)
    {
        var pkgA = ExtractAllPackageVersions(buildA);
        var pkgB = ExtractAllPackageVersions(buildB);

        var allNames = new HashSet<string>(pkgA.Keys, StringComparer.OrdinalIgnoreCase);
        allNames.UnionWith(pkgB.Keys);

        var diffs = new List<PackageDiff>();
        foreach (var name in allNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            pkgA.TryGetValue(name, out var verA);
            pkgB.TryGetValue(name, out var verB);

            if (!string.Equals(verA, verB, StringComparison.OrdinalIgnoreCase))
            {
                diffs.Add(new PackageDiff(name, verA, verB));
            }
        }

        return diffs;
    }

    private static readonly Regex PackageMessageRegex = new(
        @"^Package '(?<name>[^']+)' v(?<version>[^:]+):",
        RegexOptions.Compiled);

    private static Dictionary<string, string> ExtractAllPackageVersions(Build build)
    {
        var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Parse resolved package versions from CollectDeclaredReferencesTask messages.
        // This captures both direct and transitive dependencies with their actual resolved versions.
        // Message format: "Package '{name}' v{version}: N compile-time assembly(ies), M build file(s)"
        build.VisitAllChildren<Message>(message =>
        {
            var text = message.Text;
            if (text == null || !text.StartsWith("Package '", StringComparison.Ordinal))
                return;

            var match = PackageMessageRegex.Match(text);
            if (match.Success)
            {
                var name = match.Groups["name"].Value;
                var version = match.Groups["version"].Value;
                packages.TryAdd(name, version);
            }
        });

        return packages;
    }

    private static HashSet<string> ExtractReferenceNames(Build build, string? projectFilter)
    {
        var invocations = CompilerInvocationQuery.Execute(build, projectFilter);
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var inv in invocations)
        {
            // Parse /reference: arguments from the command line
            foreach (var arg in inv.CommandLineArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (arg.StartsWith("/reference:", StringComparison.OrdinalIgnoreCase))
                {
                    var path = arg["/reference:".Length..].Trim('"');
                    refs.Add(Path.GetFileName(path));
                }
            }
        }

        return refs;
    }
}
