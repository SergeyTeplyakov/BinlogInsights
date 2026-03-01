using BinlogInsights.Core.Models;

namespace BinlogInsights.Cli.Formatters;

public static class ItemsFormatter
{
    public static void Print(IReadOnlyList<ItemResult> items, string itemType)
    {
        if (items.Count == 0)
        {
            Console.WriteLine($"No {itemType} items found.");
            return;
        }

        Console.WriteLine($"{itemType} items ({items.Count}):");
        foreach (var item in items)
        {
            Console.WriteLine($"  {item.Include}");
            foreach (var meta in item.Metadata)
            {
                Console.WriteLine($"    {meta.Name} = {meta.Value}");
            }
        }
    }
}
