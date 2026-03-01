using BinlogInsights.Core.Models;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Core.Queries;

public static class ItemsQuery
{
    public static IReadOnlyList<ItemResult> Execute(
        Build build,
        string projectFilter,
        string itemType,
        int limit = 100,
        int offset = 0)
    {
        var evaluation = ImportsQuery.FindEvaluation(build, projectFilter);
        if (evaluation == null)
            return [];

        var items = new List<ItemResult>();

        // Items are stored in folders named after their type under evaluation
        evaluation.VisitAllChildren<AddItem>(addItem =>
        {
            if (!string.Equals(addItem.Name, itemType, StringComparison.OrdinalIgnoreCase))
                return;

            foreach (var child in addItem.Children)
            {
                if (child is Item item)
                {
                    var metadata = new List<MetadataResult>();
                    foreach (var meta in item.Children.OfType<Metadata>())
                    {
                        metadata.Add(new MetadataResult(meta.Name, meta.Value));
                    }

                    items.Add(new ItemResult(itemType, item.Text ?? item.Name ?? "", metadata));
                }
            }
        });

        // Also look for items directly (not under AddItem)
        evaluation.VisitAllChildren<Item>(item =>
        {
            // Only grab direct items that match the type by checking their parent folder
            if (item.Parent is Folder folder &&
                string.Equals(folder.Name, itemType, StringComparison.OrdinalIgnoreCase))
            {
                // Avoid duplicates from AddItem children
                if (item.Parent is AddItem) return;

                var metadata = new List<MetadataResult>();
                foreach (var meta in item.Children.OfType<Metadata>())
                {
                    metadata.Add(new MetadataResult(meta.Name, meta.Value));
                }

                items.Add(new ItemResult(itemType, item.Text ?? item.Name ?? "", metadata));
            }
        });

        return items.Skip(offset).Take(limit).ToList();
    }

    /// <summary>
    /// Returns the distinct item types available for a given project evaluation.
    /// </summary>
    public static IReadOnlyList<string> GetItemTypes(Build build, string projectFilter)
    {
        var evaluation = ImportsQuery.FindEvaluation(build, projectFilter);
        if (evaluation == null)
            return [];

        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        evaluation.VisitAllChildren<AddItem>(addItem =>
        {
            if (!string.IsNullOrEmpty(addItem.Name))
                types.Add(addItem.Name);
        });

        evaluation.VisitAllChildren<Folder>(folder =>
        {
            // Item type folders typically contain Item children
            if (folder.Children.OfType<Item>().Any() && !string.IsNullOrEmpty(folder.Name))
                types.Add(folder.Name);
        });

        return types.OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
