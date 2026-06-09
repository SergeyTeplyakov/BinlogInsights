namespace BinlogInsights.Mcp;

/// <summary>
/// Helpers for gracefully shutting down the current MCP server instance.
/// </summary>
internal static class ShutdownHelper
{
    /// <summary>
    /// Schedules a graceful self-shutdown after a short delay so the in-flight
    /// tool response can be flushed to the client before the host stops.
    /// Requests host stop first; hard-exits only if host lifetime is unavailable.
    /// </summary>
    public static async Task ShutdownAfterDelayAsync()
    {
        await Task.Delay(100).ConfigureAwait(false);
        if (!HostLifetimeBridge.TryStop())
        {
            Environment.Exit(0);
        }
    }
}
