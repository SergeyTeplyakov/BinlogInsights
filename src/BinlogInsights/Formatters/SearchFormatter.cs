using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class SearchFormatter
{
    public static void Print(IReadOnlyList<SearchResult> results, string query)
    {
        if (results.Count == 0)
        {
            Console.WriteLine($"No results found for \"{query}\".");
            return;
        }

        Console.WriteLine($"Search results for \"{query}\" ({results.Count}):");
        Console.WriteLine();

        foreach (var r in results)
        {
            var context = new List<string>();
            if (!string.IsNullOrEmpty(r.ProjectFile))
                context.Add(Path.GetFileName(r.ProjectFile));
            if (!string.IsNullOrEmpty(r.TargetName))
                context.Add(r.TargetName);
            if (!string.IsNullOrEmpty(r.TaskName))
                context.Add(r.TaskName);

            var contextStr = context.Count > 0 ? $" [{string.Join(" > ", context)}]" : "";
            Console.WriteLine($"  [{r.NodeType}]{contextStr}");
            Console.WriteLine($"    {r.Message}");
            Console.WriteLine();
        }
    }
}
