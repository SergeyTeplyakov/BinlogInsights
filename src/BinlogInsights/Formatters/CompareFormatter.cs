using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class CompareFormatter
{
    public static void Print(CompareResult result, string labelA, string labelB)
    {
        Console.WriteLine("=== Build Comparison ===");
        Console.WriteLine();

        // Overview
        PrintOverviewComparison(result.OverviewA, result.OverviewB, labelA, labelB);

        // Property diffs
        if (result.PropertyDiffs.Count > 0)
        {
            Console.WriteLine($"--- Property Differences ({result.PropertyDiffs.Count}) ---");
            Console.WriteLine();
            foreach (var diff in result.PropertyDiffs)
            {
                if (diff.ValueA == null)
                {
                    Console.WriteLine($"  {diff.Name}");
                    Console.WriteLine($"    [A] (not set)");
                    Console.WriteLine($"    [B] {diff.ValueB}");
                }
                else if (diff.ValueB == null)
                {
                    Console.WriteLine($"  {diff.Name}");
                    Console.WriteLine($"    [A] {diff.ValueA}");
                    Console.WriteLine($"    [B] (not set)");
                }
                else
                {
                    Console.WriteLine($"  {diff.Name}");
                    Console.WriteLine($"    [A] {diff.ValueA}");
                    Console.WriteLine($"    [B] {diff.ValueB}");
                }
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("--- Properties: identical ---");
            Console.WriteLine();
        }

        // Import diffs
        if (result.ImportsOnlyInA.Count > 0 || result.ImportsOnlyInB.Count > 0)
        {
            Console.WriteLine("--- Import Differences ---");
            Console.WriteLine();
            if (result.ImportsOnlyInA.Count > 0)
            {
                Console.WriteLine($"  Only in [A] ({result.ImportsOnlyInA.Count}):");
                foreach (var f in result.ImportsOnlyInA)
                    Console.WriteLine($"    - {f}");
                Console.WriteLine();
            }
            if (result.ImportsOnlyInB.Count > 0)
            {
                Console.WriteLine($"  Only in [B] ({result.ImportsOnlyInB.Count}):");
                foreach (var f in result.ImportsOnlyInB)
                    Console.WriteLine($"    + {f}");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("--- Imports: identical ---");
            Console.WriteLine();
        }

        // Package diffs (project-scoped)
        PrintPackageDiffs(result.PackageDiffs, "NuGet Package Differences (project)");

        // Package diffs (solution-wide)
        PrintPackageDiffs(result.SolutionPackageDiffs, "NuGet Package Differences (all projects)");

        // Reference diffs
        if (result.ReferencesOnlyInA.Count > 0 || result.ReferencesOnlyInB.Count > 0)
        {
            Console.WriteLine("--- Compiler Reference Differences ---");
            Console.WriteLine();
            if (result.ReferencesOnlyInA.Count > 0)
            {
                Console.WriteLine($"  Only in [A] ({result.ReferencesOnlyInA.Count}):");
                foreach (var r in result.ReferencesOnlyInA)
                    Console.WriteLine($"    - {r}");
                Console.WriteLine();
            }
            if (result.ReferencesOnlyInB.Count > 0)
            {
                Console.WriteLine($"  Only in [B] ({result.ReferencesOnlyInB.Count}):");
                foreach (var r in result.ReferencesOnlyInB)
                    Console.WriteLine($"    + {r}");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine("--- References: identical ---");
            Console.WriteLine();
        }
    }

    private static void PrintOverviewComparison(
        BuildOverviewResult a, BuildOverviewResult b,
        string labelA, string labelB)
    {
        Console.WriteLine("--- Overview ---");
        Console.WriteLine();
        Console.WriteLine($"  {"",30}  {"[A] " + labelA,-40}  {"[B] " + labelB}");
        Console.WriteLine($"  {"Build result",-30}  {(a.Succeeded ? "SUCCEEDED" : "FAILED"),-40}  {(b.Succeeded ? "SUCCEEDED" : "FAILED")}");
        Console.WriteLine($"  {"Duration",-30}  {a.Duration.TotalSeconds:F1}s{"",-35}  {b.Duration.TotalSeconds:F1}s");
        Console.WriteLine($"  {"Projects",-30}  {a.ProjectCount,-40}  {b.ProjectCount}");
        Console.WriteLine($"  {"Errors",-30}  {a.ErrorCount,-40}  {b.ErrorCount}");
        Console.WriteLine($"  {"Warnings",-30}  {a.WarningCount,-40}  {b.WarningCount}");
        Console.WriteLine($"  {"MSBuild version",-30}  {a.MsBuildVersion ?? "?",-40}  {b.MsBuildVersion ?? "?"}");
        Console.WriteLine();
    }

    private static void PrintPackageDiffs(IReadOnlyList<PackageDiff> diffs, string sectionTitle)
    {
        if (diffs.Count > 0)
        {
            var versionDiffs = diffs.Where(p => p.VersionA != null && p.VersionB != null).ToList();
            var onlyInA = diffs.Where(p => p.VersionB == null).ToList();
            var onlyInB = diffs.Where(p => p.VersionA == null).ToList();

            Console.WriteLine($"--- {sectionTitle} ({diffs.Count}) ---");
            Console.WriteLine();

            if (versionDiffs.Count > 0)
            {
                Console.WriteLine($"  Version differences ({versionDiffs.Count}):");
                var maxName = versionDiffs.Max(d => d.PackageName.Length);
                foreach (var diff in versionDiffs)
                    Console.WriteLine($"    {diff.PackageName.PadRight(maxName)}  {diff.VersionA,-20} -> {diff.VersionB}");
                Console.WriteLine();
            }

            if (onlyInA.Count > 0)
            {
                Console.WriteLine($"  Only in [A] ({onlyInA.Count}):");
                foreach (var p in onlyInA)
                    Console.WriteLine($"    - {p.PackageName} {p.VersionA}");
                Console.WriteLine();
            }

            if (onlyInB.Count > 0)
            {
                Console.WriteLine($"  Only in [B] ({onlyInB.Count}):");
                foreach (var p in onlyInB)
                    Console.WriteLine($"    + {p.PackageName} {p.VersionB}");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine($"--- {sectionTitle}: identical ---");
            Console.WriteLine();
        }
    }
}
