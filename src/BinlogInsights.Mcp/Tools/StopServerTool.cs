
using System.ComponentModel;
using ModelContextProtocol.Server;


namespace BinlogInsights.Mcp;

/// <summary>
/// MCP tool to gracefully stop the server via protocol request.
/// </summary>
[McpServerToolType]
public class StopServerTool
{
    [McpServerTool(Name = "stop", Title = "Stop MCP Server", ReadOnly = false, Idempotent = false)]
    [Description("Stops only this MCP server instance (for upgrades or local iteration).")]
    public static string Execute()
    {
        // Graceful self-shutdown: schedule the stop so the response is flushed to the client first.
        _ = ShutdownHelper.ShutdownAfterDelayAsync();
        return "Stopping this MCP server instance.";
    }
}
