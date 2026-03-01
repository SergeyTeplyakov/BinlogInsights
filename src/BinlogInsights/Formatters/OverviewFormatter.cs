using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class OverviewFormatter
{
    public static void Print(BuildOverviewResult result)
    {
        Console.WriteLine($"Build: {(result.Succeeded ? "SUCCEEDED" : "FAILED")}");
        Console.WriteLine($"MSBuild version: {result.MsBuildVersion ?? "(unknown)"}");
        Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
        Console.WriteLine($"Projects: {result.ProjectCount}");
        Console.WriteLine($"Errors: {result.ErrorCount}");
        Console.WriteLine($"Warnings: {result.WarningCount}");
        Console.WriteLine();
        Console.WriteLine("Projects:");
        foreach (var p in result.Projects)
        {
            var status = p.Succeeded ? "OK" : "FAILED";
            var tf = !string.IsNullOrEmpty(p.TargetFramework) ? $" [{p.TargetFramework}]" : "";
            Console.WriteLine($"  [{status}] {p.ProjectFile}{tf} ({p.Duration.TotalSeconds:F1}s)");
        }
    }
}
