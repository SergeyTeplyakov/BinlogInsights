using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class PropertiesFormatter
{
    public static void Print(IReadOnlyList<PropertyResult> properties)
    {
        if (properties.Count == 0)
        {
            Console.WriteLine("No matching properties found.");
            return;
        }

        Console.WriteLine($"Properties ({properties.Count}):");
        var maxNameLen = properties.Max(p => p.Name.Length);
        foreach (var p in properties)
        {
            Console.WriteLine($"  {p.Name.PadRight(maxNameLen)} = {p.Value}");
        }
    }
}
