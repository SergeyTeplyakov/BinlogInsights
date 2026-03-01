namespace BinlogInsights.Core.Models;

public record PropertyResult(string Name, string Value);

public record ItemResult(string ItemType, string Include, IReadOnlyList<MetadataResult> Metadata);

public record MetadataResult(string Name, string Value);
