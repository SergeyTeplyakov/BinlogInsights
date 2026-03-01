using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class ImportsFormatter
{
    public static void Print(IReadOnlyList<ImportResult> imports)
    {
        if (imports.Count == 0)
        {
            Console.WriteLine("No imports found.");
            return;
        }

        Console.WriteLine("Import tree:");
        PrintLevel(imports, indent: 0);
    }

    private static void PrintLevel(IReadOnlyList<ImportResult> imports, int indent)
    {
        var prefix = new string(' ', indent * 2);
        foreach (var import in imports)
        {
            var marker = import.IsMissing ? "[MISSING] " : "";
            var location = import.Line.HasValue ? $" (line {import.Line})" : "";
            Console.WriteLine($"{prefix}{marker}{import.ImportedFile}{location}");
            if (import.Children.Count > 0)
                PrintLevel(import.Children, indent + 1);
        }
    }
}
