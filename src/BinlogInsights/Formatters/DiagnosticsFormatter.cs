using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class DiagnosticsFormatter
{
    public static void Print(IReadOnlyList<DiagnosticResult> diagnostics, string severity, int totalCount, int offset, int limit)
    {
        if (diagnostics.Count == 0)
        {
            Console.WriteLine($"No {severity}s found.");
            return;
        }

        Console.WriteLine($"Showing {severity}s {offset + 1}-{offset + diagnostics.Count} of {totalCount}:");
        Console.WriteLine();

        foreach (var d in diagnostics)
        {
            var code = !string.IsNullOrEmpty(d.Code) ? $" {d.Code}" : "";
            var context = FormatContext(d.ProjectFile, d.TargetName, d.TaskName);
            Console.WriteLine($"{severity.ToUpperInvariant()}{code}{context}");
            Console.WriteLine($"  Message: {d.Message}");
            if (!string.IsNullOrEmpty(d.File))
            {
                var location = d.File;
                if (d.Line.HasValue)
                    location += $"({d.Line}{(d.Column.HasValue ? $",{d.Column}" : "")})";
                Console.WriteLine($"  File: {location}");
            }
            Console.WriteLine();
        }

        var remaining = totalCount - offset - diagnostics.Count;
        if (remaining > 0)
        {
            Console.WriteLine($"[... {remaining} more {severity}s. Use --offset {offset + limit} to see next page.]");
        }
    }

    private static string FormatContext(string? project, string? target, string? task)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(project))
            parts.Add(Path.GetFileName(project));
        if (!string.IsNullOrEmpty(target))
            parts.Add(target);
        if (!string.IsNullOrEmpty(task))
            parts.Add(task);
        return parts.Count > 0 ? $" in {string.Join(" > ", parts)}" : "";
    }
}
