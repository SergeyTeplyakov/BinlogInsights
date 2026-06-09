using System.ComponentModel;
using System.Text;
using System.Threading;
using BinlogInsights.Mcp.Search;
using Microsoft.Build.Logging.StructuredLogger;
using ModelContextProtocol.Server;
using StructuredLogViewer;

namespace BinlogInsights.Mcp;

/// <summary>
/// Viewer-syntax search over a binlog: returns matching nodes as an indented tree,
/// mirroring what the Structured Log Viewer shows in its Search panel.
/// </summary>
[McpServerToolType]
public class QueryTool
{
    internal const int DefaultMaxResults = 200;
    internal const int MaxAllowedResults = 5000;
    private const int IndentSpaces = 2;

    [McpServerTool(Name = "binlog_query", Title = "Query Binlog (Viewer Syntax)",
        ReadOnly = true, Idempotent = true)]
    [Description(@"Searches a binlog using the MSBuild Structured Log Viewer query syntax and returns matching nodes as an indented tree, mirroring the viewer's Search panel.

Real (addressable) nodes have their id appended in square brackets, e.g.
  Project StructuredLogger.csproj net10.0 → Build [123]
    Target CoreCompile [124]
      Task Csc [125]
Use those ids with binlog_get_node, binlog_get_children, binlog_get_ancestors, binlog_print_subtree.
Ids are stable for the same binlog file bytes but not portable across files; discard them once the file is overwritten by a new build.

Query syntax cheat sheet (call binlog_search_syntax_help for the full reference):
  $error                          all errors
  $warning                        all warnings
  $task Csc                       all Csc task invocations
  $target Build                   all Build targets
  $project MyProj                 projects whose name contains MyProj
  under($project MyProj) CS1234   nodes containing CS1234 under MyProj
  notunder($task Csc) error       errors not under a Csc task
  $task $time                     tasks, with durations, sorted slowest first
  ""exact phrase""                literal substring match
  name=Configuration value=Debug  precise field match
  $42                             node with Index 42

Paging: results are computed up to offset + limit, then sliced. A trailing '+' on the matched count means the search hit the cap — use binlog_count for a true total.")]
    public static string Execute(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Search query in MSBuild Structured Log Viewer syntax")] string query,
        [Description("Maximum number of results to return (default 200, max 5000)")] int limit = DefaultMaxResults,
        [Description("Number of leading results to skip for paging (default 0)")] int offset = 0)
    {
        int take = Math.Clamp(limit, 1, MaxAllowedResults);
        int skip = Math.Max(offset, 0);

        var build = cache.Load(binlog_file);
        var index = SearchState.For(build).Index;

        // SearchIndex.FindNodes is not thread-safe (mutates typeKeyword,
        // bit vector). Serialize calls per index.
        IReadOnlyList<SearchResult> results;
        lock (index)
        {
            index.MaxResults = skip + take;
            results = index.FindNodes(query, CancellationToken.None).ToArray();
        }

        int total = results.Count;
        var page = results.Skip(skip).Take(take).ToArray();

        var sb = new StringBuilder();
        sb.Append(total).Append(total == 1 ? " result" : " results");
        sb.Append(" (skip=").Append(skip)
          .Append(", take=").Append(take)
          .Append(", matched=").Append(total);
        if (total >= skip + take)
        {
            sb.Append('+');
        }

        sb.AppendLine(")");

        if (page.Length == 0)
        {
            sb.AppendLine("(no results)");
            return sb.ToString();
        }

        // ResultTree groups results under their nearest Project/Target/Task,
        // mirroring the viewer's Search panel. addDuration=false suppresses
        // its own "N results" Note since we already emit a header line.
        var tree = ResultTree.BuildResultTree(page, addDuration: false);
        foreach (var child in tree.Children)
        {
            AppendNode(sb, child, depth: 0);
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "binlog_count", Title = "Count Query Matches",
        ReadOnly = true, Idempotent = true)]
    [Description(@"Counts the total number of nodes matching a query, with no result cap. Returns a single line: 'matched: N'.

Use this when binlog_query returns a capped 'matched=N+' and you need the true total — cheaper than running binlog_query with a huge limit because no tree formatting is done. Same query DSL as binlog_query.")]
    public static string Count(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Search query in MSBuild Structured Log Viewer syntax")] string query)
    {
        var build = cache.Load(binlog_file);
        var index = SearchState.For(build).Index;

        int total;
        lock (index)
        {
            int saved = index.MaxResults;
            try
            {
                index.MaxResults = int.MaxValue;
                total = index.FindNodes(query, CancellationToken.None).Count();
            }
            finally
            {
                index.MaxResults = saved;
            }
        }

        return $"matched: {total}";
    }

    /// <summary>
    /// Appends start/end/duration fields when the query opted into them via
    /// $start, $end, or $time/$duration. Mirrors the viewer's ProxyNode.AddDuration output.
    /// </summary>
    private static void AppendTimeFields(StringBuilder sb, SearchResult? result)
    {
        if (result is null)
        {
            return;
        }

        if (result.StartTime != default)
        {
            string text = TextUtilities.Display(result.StartTime, fullPrecision: true);
            if (!string.IsNullOrEmpty(text))
            {
                sb.Append(" start=").Append(text);
            }
        }

        if (result.EndTime != default)
        {
            string text = TextUtilities.Display(result.EndTime, fullPrecision: true);
            if (!string.IsNullOrEmpty(text))
            {
                sb.Append(" end=").Append(text);
            }
        }

        if (result.Duration != default)
        {
            string text = TextUtilities.DisplayDuration(result.Duration);
            if (!string.IsNullOrEmpty(text))
            {
                sb.Append(" duration=").Append(text);
            }
        }
    }

    /// <summary>
    /// Pretty-prints a search result subtree. <see cref="ProxyNode"/> wrappers (created by
    /// <see cref="ResultTree.BuildResultTree(object, TimeSpan, TimeSpan, bool, Func{BaseNode})"/>)
    /// are unwrapped so real nodes get their <c>[id]</c> appended; everything else prints verbatim.
    /// </summary>
    private static void AppendNode(StringBuilder sb, BaseNode node, int depth)
    {
        sb.Append(' ', depth * IndentSpaces);

        if (node is ProxyNode proxy)
        {
            if (proxy.Original is NameValueNode nv)
            {
                string name = TextUtilities.ShortenValue(nv.Name ?? string.Empty, "...", maxChars: 300);
                string value = TextUtilities.ShortenValue(nv.Value ?? string.Empty, "...", maxChars: 300);
                sb.Append(name).Append('=').Append(value);
                string? id = NodeId.Get(nv);
                if (id is not null)
                {
                    sb.Append(" [").Append(id).Append(']');
                }

                AppendTimeFields(sb, proxy.SearchResult);
                sb.AppendLine();
            }
            else
            {
                // FileCopy results carry a Kind ("Source" / "Destination" /
                // "SourceAndDestination") that the viewer renders as an icon.
                if (proxy.Original is FileCopy { Kind.Length: > 0 } fileCopy)
                {
                    string tag = fileCopy.Kind switch
                    {
                        "Source" => "[Source]",
                        "Destination" => "[Destination]",
                        "SourceAndDestination" => "[Source+Destination]",
                        _ => "[" + fileCopy.Kind + "]"
                    };
                    sb.Append(tag).Append(' ');
                }

                string text = (proxy.Text ?? string.Empty).TrimEnd();
                sb.Append(text);
                if (proxy.Original is { } original)
                {
                    string? id = NodeId.Get(original);
                    if (id is not null)
                    {
                        sb.Append(" [").Append(id).Append(']');
                    }
                }

                AppendTimeFields(sb, proxy.SearchResult);
                sb.AppendLine();
            }
        }
        else
        {
            string text = (node.GetFullText() ?? node.Title ?? string.Empty).TrimEnd();
            sb.AppendLine(text);
        }

        if (node is TreeNode { HasChildren: true } treeNode)
        {
            foreach (var child in treeNode.Children)
            {
                AppendNode(sb, child, depth + 1);
            }
        }
    }
}

/// <summary>
/// Tree-navigation tools built on the round-trippable node ids returned by binlog_query.
/// </summary>
[McpServerToolType]
public class NavigationTool
{
    private const int DefaultPrintMaxNodes = 500;
    private const int MaxAllowedPrintNodes = 10000;

    [McpServerTool(Name = "binlog_get_node", Title = "Get Node Metadata",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns metadata for a single node: kind, full (untruncated) text, parent id, child count, source location, and (for timed nodes) start/end/duration. Does not return children — use binlog_get_children. The id comes from binlog_query.")]
    public static string GetNode(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Node id as returned by binlog_query")] string id)
    {
        var build = cache.Load(binlog_file);
        var state = SearchState.For(build);
        var node = NodeId.Resolve(state.IndexMap, id);
        return NodeFormatter.DescribeNode(node);
    }

    [McpServerTool(Name = "binlog_get_children", Title = "Get Node Children",
        ReadOnly = true, Idempotent = true)]
    [Description(@"Returns the immediate children of a node, optionally filtered by kind and/or a name substring, paginated. Each line is: 'kind summary [id]'.

Filtering avoids paging through thousands of children just to find a few interesting ones (e.g. a Project with 10,000 Properties + 200 Targets).

kind: any $-token from the search DSL minus the leading '$' (e.g. ""target"", ""task"", ""property"", ""item"", ""metadata"", ""message"", ""error"", ""warning"", ""csc"", ""rar""). Same matching rules as binlog_query.

name_contains: raw search text matched against the child's name/text fields with the same semantics as binlog_query. Plain text is substring search; include quotes yourself for exact matching. Use kind for node-type filtering, not name_contains.")]
    public static string GetChildren(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Node id as returned by binlog_query")] string id,
        [Description("Optional node-kind filter, e.g. \"target\", \"task\", \"property\", \"csc\" (the search DSL's $kind tokens without the $)")] string? kind = null,
        [Description("Optional raw search text matched against each child's name/text. Plain text is substring search; quote for exact match")] string? name_contains = null,
        [Description("Number of leading children (after filtering) to skip (default 0)")] int offset = 0,
        [Description("Maximum number of children to return (default 200, max 5000)")] int limit = QueryTool.DefaultMaxResults)
    {
        int skip = Math.Max(offset, 0);
        int take = Math.Clamp(limit, 1, QueryTool.MaxAllowedResults);

        var build = cache.Load(binlog_file);
        var state = SearchState.For(build);
        var node = NodeId.Resolve(state.IndexMap, id);

        if (node is not TreeNode { HasChildren: true } tree)
        {
            return $"node [{id}] has no children";
        }

        kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim().TrimStart('$');
        name_contains = string.IsNullOrWhiteSpace(name_contains) ? null : name_contains.Trim();

        NodeQueryMatcher? matcher = null;
        string? filterDescription = null;
        if (kind is not null || name_contains is not null)
        {
            var queryParts = new List<string>(2);
            if (kind is not null)
            {
                queryParts.Add("$" + kind);
            }

            if (name_contains is not null)
            {
                queryParts.Add(name_contains);
            }

            string query = string.Join(" ", queryParts);
            matcher = new NodeQueryMatcher(query);
            filterDescription = query;
        }

        IEnumerable<BaseNode> source = tree.Children;
        if (matcher is not null)
        {
            source = source.Where(c => matcher.IsMatch(c) is not null);
        }

        var filtered = source.ToList();
        int total = filtered.Count;

        var sb = new StringBuilder();
        sb.Append("parent: ").AppendLine(NodeFormatter.FormatNode(node));
        if (filterDescription is not null)
        {
            sb.Append("filter: ").AppendLine(filterDescription);
        }

        sb.Append("children: ").Append(Math.Min(take, Math.Max(0, total - skip)))
          .Append(" (skip=").Append(skip)
          .Append(", take=").Append(take)
          .Append(", total=").Append(total);
        if (matcher is not null)
        {
            sb.Append(", unfiltered=").Append(tree.Children.Count);
        }

        sb.AppendLine(")");

        int end = Math.Min(total, skip + take);
        for (int i = skip; i < end; i++)
        {
            sb.AppendLine(NodeFormatter.FormatNode(filtered[i]));
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "binlog_get_ancestors", Title = "Get Node Ancestors",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns the chain of ancestors of a node from the root down to (but not including) the node itself. Each line is: 'kind summary [id]'. Useful for answering 'where did this happen?'")]
    public static string GetAncestors(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Node id as returned by binlog_query")] string id)
    {
        var build = cache.Load(binlog_file);
        var state = SearchState.For(build);
        var node = NodeId.Resolve(state.IndexMap, id);

        var chain = new List<BaseNode>();
        var current = node.Parent;
        while (current is not null)
        {
            chain.Add(current);
            current = current.Parent;
        }

        chain.Reverse();

        if (chain.Count == 0)
        {
            return $"node [{id}] has no ancestors (it is the root)";
        }

        var sb = new StringBuilder();
        sb.Append("ancestors of ").Append(NodeFormatter.FormatNode(node)).Append(": ").Append(chain.Count).AppendLine();
        foreach (var ancestor in chain)
        {
            sb.AppendLine(NodeFormatter.FormatNode(ancestor));
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "binlog_print_subtree", Title = "Print Node Subtree",
        ReadOnly = true, Idempotent = true)]
    [Description(@"Renders a node and its descendants as indented text, viewer-style. Each line is: 'kind summary [id]'.

When max_nodes is hit, the trailing hint suggests two ways to continue:
  - drill in: binlog_get_children on the truncation point (deeper)
  - continue level: binlog_get_children on its parent with offset=N (more siblings)
When max_depth is hit, an inline '... N more' marker shows the suppressed children.")]
    public static string PrintSubtree(
        BinlogCache cache,
        [Description("Path to the .binlog file")] string binlog_file,
        [Description("Node id as returned by binlog_query")] string id,
        [Description("Maximum tree depth to render relative to the root node (default unlimited)")] int? max_depth = null,
        [Description("Maximum number of nodes to render (default 500, max 10000)")] int? max_nodes = null)
    {
        int nodeBudget = Math.Clamp(max_nodes ?? DefaultPrintMaxNodes, 1, MaxAllowedPrintNodes);
        int depthLimit = max_depth ?? int.MaxValue;

        var build = cache.Load(binlog_file);
        var state = SearchState.For(build);
        var node = NodeId.Resolve(state.IndexMap, id);

        var sb = new StringBuilder();
        int rendered = 0;
        bool truncated = false;
        string? truncationHint = null;

        void Write(BaseNode n, int depth, BaseNode? parent, int indexInParent)
        {
            if (truncated)
            {
                return;
            }

            if (rendered >= nodeBudget)
            {
                truncated = true;
                string nId = NodeId.Get(n) ?? "?";
                var hintBuilder = new StringBuilder();
                hintBuilder.Append("truncated at max_nodes=").Append(nodeBudget).AppendLine(".");
                hintBuilder.Append("  to drill in:        binlog_get_children(id=\"").Append(nId).AppendLine("\")");
                if (parent is not null && indexInParent >= 0)
                {
                    string? parentId = NodeId.Get(parent);
                    if (parentId is not null)
                    {
                        hintBuilder.Append("  to continue level:  binlog_get_children(id=\"")
                            .Append(parentId).Append("\", offset=").Append(indexInParent).AppendLine(")");
                    }
                }

                truncationHint = hintBuilder.ToString().TrimEnd();
                return;
            }

            rendered++;
            sb.Append(' ', depth * 2);
            sb.AppendLine(NodeFormatter.FormatNode(n));

            if (depth >= depthLimit)
            {
                if (n is TreeNode { HasChildren: true } tn && !truncated)
                {
                    string nodeIdText = NodeId.Get(n) ?? "?";
                    sb.Append(' ', (depth + 1) * 2);
                    sb.Append("... ").Append(tn.Children.Count).Append(" more (depth limit; call binlog_get_children(id=\"")
                      .Append(nodeIdText).AppendLine("\") to drill in)");
                }

                return;
            }

            if (n is TreeNode { HasChildren: true } tree)
            {
                for (int i = 0; i < tree.Children.Count; i++)
                {
                    Write(tree.Children[i], depth + 1, n, i);
                    if (truncated)
                    {
                        return;
                    }
                }
            }
        }

        Write(node, 0, parent: null, indexInParent: -1);

        if (truncationHint is not null)
        {
            sb.AppendLine(truncationHint);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Exposes the full Structured Log Viewer search DSL reference to LLM clients on demand.
/// </summary>
[McpServerToolType]
public class SearchSyntaxHelpTool
{
    private const string ResourceName = "SearchSyntax.md";
    private static string? s_text;

    [McpServerTool(Name = "binlog_search_syntax_help", Title = "Binlog Query Syntax Help",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns the full MSBuild Structured Log Viewer search query syntax reference used by binlog_query, binlog_count and binlog_get_children: node-kind filters ($error, $task, ...), field filters, under()/notunder()/project() scoping, time filters/annotations, and specialized indexes ($copy, $nuget, $projectreference).")]
    public static string Execute()
    {
        if (s_text is not null)
        {
            return s_text;
        }

        var assembly = typeof(SearchSyntaxHelpTool).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");
        using var reader = new StreamReader(stream);
        s_text = reader.ReadToEnd();
        return s_text;
    }
}
