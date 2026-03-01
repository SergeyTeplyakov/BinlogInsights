using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class CompilerInvocationQuery
{
    public static IReadOnlyList<CompilerInvocationResult> Execute(Build build, string? projectFilter = null)
    {
        var invocations = CompilerInvocationsReader.ReadInvocations(build);
        var results = new List<CompilerInvocationResult>();

        foreach (var inv in invocations)
        {
            if (!string.IsNullOrEmpty(projectFilter) &&
                (inv.ProjectFilePath == null ||
                 !inv.ProjectFilePath.Contains(projectFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            results.Add(new CompilerInvocationResult(
                Language: inv.Language,
                ProjectFile: inv.ProjectFilePath ?? "(unknown)",
                CommandLineArguments: inv.CommandLineArguments));
        }

        return results;
    }
}
