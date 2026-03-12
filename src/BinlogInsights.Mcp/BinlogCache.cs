using System.Collections.Concurrent;
using Microsoft.Build.Logging.StructuredLogger;
using BinlogInsights.Core;

namespace BinlogInsights.Mcp;

/// <summary>
/// Caches loaded Build objects by file path, invalidating when the file changes.
/// Uses last-write-time + file size to detect changes reliably (timestamp alone
/// can be preserved when files are copied/replaced).
/// </summary>
public class BinlogCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public Build Load(string binlogPath)
    {
        var fullPath = Path.GetFullPath(binlogPath);
        var fileInfo = new FileInfo(fullPath);
        var lastWrite = fileInfo.LastWriteTimeUtc;
        var length = fileInfo.Length;

        if (_cache.TryGetValue(fullPath, out var entry)
            && entry.LastWriteTime == lastWrite
            && entry.FileSize == length)
        {
            return entry.Build;
        }

        var build = BinlogAnalyzer.LoadBuild(fullPath);
        _cache[fullPath] = new CacheEntry(build, lastWrite, length);
        return build;
    }

    private sealed record CacheEntry(Build Build, DateTime LastWriteTime, long FileSize);
}
