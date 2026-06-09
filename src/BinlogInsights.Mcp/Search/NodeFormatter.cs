using System.Text;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogInsights.Mcp.Search;

/// <summary>
/// Uniform one-line and multi-line text rendering for <see cref="BaseNode"/> instances,
/// shared by the query and tree-navigation tools so output is consistent and the trailing
/// <c>[id]</c> produced here always round-trips through <see cref="NodeId"/>.
/// </summary>
internal static class NodeFormatter
{
    private const int MaxSummaryChars = 300;

    private static string Summarize(BaseNode node)
    {
        string text = node.GetFullText() ?? node.Title ?? node.ToString() ?? string.Empty;
        return TextUtilities.ShortenValue(text, "...", maxChars: MaxSummaryChars);
    }

    /// <summary>
    /// The canonical one-line representation of a node: <c>"Kind Summary [id]"</c>.
    /// <see cref="Property"/> and <see cref="Metadata"/> nodes use <c>Name=Value [id]</c>
    /// instead, since their kind is implied by context and the value is the interesting part.
    /// </summary>
    public static string FormatNode(BaseNode node)
    {
        string id = NodeId.Get(node) ?? "?";
        if (node is Property or Metadata)
        {
            var nv = (NameValueNode)node;
            string value = TextUtilities.ShortenValue(nv.Value ?? string.Empty, "...", maxChars: MaxSummaryChars);
            return $"{nv.Name ?? string.Empty}={value} [{id}]";
        }

        // FileCopy results carry a Kind that the viewer renders as a colored icon
        // (Source / Destination / both). Prepend it as a tag so the LLM sees the same signal.
        string copyTag = string.Empty;
        if (node is FileCopy fileCopy && !string.IsNullOrEmpty(fileCopy.Kind))
        {
            copyTag = fileCopy.Kind switch
            {
                "Source" => "[Source] ",
                "Destination" => "[Destination] ",
                "SourceAndDestination" => "[Source+Destination] ",
                _ => "[" + fileCopy.Kind + "] "
            };
        }

        string kind = node.TypeName ?? node.GetType().Name;
        string summary = Summarize(node);

        // Some nodes (Import, NoImport) already include their kind in GetFullText
        // (e.g. "Import Foo.targets at (1;1)"). Don't double it up.
        if (summary.StartsWith(kind + " ", StringComparison.Ordinal))
        {
            return $"{copyTag}{summary} [{id}]";
        }

        return $"{copyTag}{kind} {summary} [{id}]";
    }

    /// <summary>
    /// Multi-line description of a single node with full (untruncated) text plus metadata:
    /// timing for <see cref="TimedNode"/>, source location, parent, and child count.
    /// </summary>
    public static string DescribeNode(BaseNode node)
    {
        var sb = new StringBuilder();
        string kind = node.TypeName ?? node.GetType().Name;
        string fullText = node.GetFullText() ?? node.Title ?? node.ToString() ?? string.Empty;
        string id = NodeId.Get(node) ?? "?";
        if (fullText.StartsWith(kind + " ", StringComparison.Ordinal))
        {
            sb.Append(fullText).Append(" [").Append(id).Append(']').AppendLine();
        }
        else
        {
            sb.Append(kind).Append(' ').Append(fullText).Append(" [").Append(id).Append(']').AppendLine();
        }

        if (node is TimedNode timed)
        {
            sb.Append("start: ").AppendLine(timed.StartTime.ToString("o"));
            sb.Append("end: ").AppendLine(timed.EndTime.ToString("o"));
            sb.Append("duration: ").AppendLine(timed.Duration.ToString());
        }

        if (node is IHasSourceFile { SourceFilePath: { Length: > 0 } sourceFile })
        {
            sb.Append("sourceFile: ").AppendLine(sourceFile);
        }

        if (node is IHasLineNumber { LineNumber: int lineNumber })
        {
            sb.Append("line: ").AppendLine(lineNumber.ToString());
        }

        if (node.Parent is BaseNode parent)
        {
            sb.Append("parent: ").AppendLine(FormatNode(parent));
        }

        if (node is TreeNode tree)
        {
            sb.Append("childCount: ").AppendLine(tree.Children.Count.ToString());
        }

        return sb.ToString();
    }
}
