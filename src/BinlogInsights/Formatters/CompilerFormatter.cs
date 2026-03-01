using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class CompilerFormatter
{
    public static void Print(IReadOnlyList<CompilerInvocationResult> invocations)
    {
        if (invocations.Count == 0)
        {
            Console.WriteLine("No compiler invocations found.");
            return;
        }

        foreach (var inv in invocations)
        {
            Console.WriteLine($"[{inv.Language}] {inv.ProjectFile}");
            Console.WriteLine($"  {inv.CommandLineArguments}");
            Console.WriteLine();
        }
    }
}
