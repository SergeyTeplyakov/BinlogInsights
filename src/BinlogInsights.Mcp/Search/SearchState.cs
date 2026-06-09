using System.Runtime.CompilerServices;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer;

namespace BinlogInsights.Mcp.Search;

/// <summary>
/// Lazily builds and caches the search infrastructure for a loaded <see cref="Build"/>:
/// the <see cref="StructuredLogger.SearchIndex"/> used by the viewer's query DSL and a
/// dense <c>Index → TimedNode</c> map used to resolve round-trippable node ids.
/// <para>
/// Cached per <see cref="Build"/> instance via a <see cref="ConditionalWeakTable{TKey,TValue}"/>
/// so it is collected together with the Build when the cache evicts it. The factory runs
/// once per build, so the (mutating) one-time wiring below is safe under concurrent calls.
/// </para>
/// </summary>
internal sealed class SearchState
{
    private static readonly ConditionalWeakTable<Build, SearchState> Table = new();

    public SearchIndex Index { get; }

    /// <summary>
    /// Lookup from <see cref="TimedNode.Index"/> to node. Indices are assigned densely
    /// from 0 by <c>BuildAnalyzer</c>, so a flat array is the natural representation.
    /// </summary>
    public TimedNode[] IndexMap { get; }

    private SearchState(Build build)
    {
        if (build.SearchIndex is null)
        {
            build.SearchIndex = new SearchIndex(build);

            // Match the viewer: register the optional search extensions so
            // queries like `$secret` and `$nuget` work out of the box.
            build.SearchExtensions.Add(new SecretsSearch(build));
            build.SearchExtensions.Add(new NuGetSearch(build));
        }

        Index = build.SearchIndex;

        var list = new List<TimedNode>();
        build.VisitAllChildren<TimedNode>(node =>
        {
            int i = node.Index;
            while (list.Count <= i)
            {
                list.Add(null!);
            }

            list[i] = node;
        });

        IndexMap = list.ToArray();
    }

    public static SearchState For(Build build) => Table.GetValue(build, static b => new SearchState(b));
}
