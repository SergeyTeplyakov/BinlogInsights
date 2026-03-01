namespace BinlogInsights.Core.Models;

public record CompilerInvocationResult(
    string Language,
    string ProjectFile,
    string CommandLineArguments);
