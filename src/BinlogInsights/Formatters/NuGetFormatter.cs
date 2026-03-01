using BinlogInsights.Core.Queries;

namespace BinlogInsights.Cli.Formatters;

public static class NuGetFormatter
{
    public static void Print(NuGetRestoreResult result)
    {
        Console.WriteLine($"NuGet Restore: {(result.RestoreSucceeded ? "SUCCEEDED" : "FAILED")}");
        Console.WriteLine();

        if (result.RestoreMessages.Count > 0)
        {
            Console.WriteLine("Restore messages:");
            foreach (var msg in result.RestoreMessages)
            {
                var code = !string.IsNullOrEmpty(msg.Code) ? $" {msg.Code}" : "";
                Console.WriteLine($"  [{msg.Severity.ToUpperInvariant()}{code}] {msg.Message}");
            }
            Console.WriteLine();
        }

        if (result.PackageReferences.Count > 0)
        {
            Console.WriteLine($"PackageReference items ({result.PackageReferences.Count}):");
            foreach (var pkg in result.PackageReferences)
            {
                var version = pkg.Metadata.FirstOrDefault(m =>
                    string.Equals(m.Name, "Version", StringComparison.OrdinalIgnoreCase));
                var versionStr = version != null ? $" ({version.Value})" : "";
                Console.WriteLine($"  {pkg.Include}{versionStr}");
            }
            Console.WriteLine();
        }

        if (result.NuGetProperties.Count > 0)
        {
            Console.WriteLine("NuGet properties:");
            foreach (var prop in result.NuGetProperties)
            {
                Console.WriteLine($"  {prop.Name} = {prop.Value}");
            }
        }
    }
}
