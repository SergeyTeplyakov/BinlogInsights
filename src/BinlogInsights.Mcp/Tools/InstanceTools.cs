using System.ComponentModel;
using System.Diagnostics;
using BinlogInsights.Mcp.Commands;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

/// <summary>
/// Discovery tool: lists this MCP instance plus any other running binlog-insights-mcp instances on the machine.
/// Designed for an LLM-driven shutdown workflow: discover instances -> stop_instance(pid) for each.
/// </summary>
[McpServerToolType]
public class ListMcpInstancesTool
{
    [McpServerTool(Name = "list_mcp_instances", Title = "List MCP Instances",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all running binlog-insights-mcp instances on this machine, including the current one. " +
                 "Use this before stop_instance to enumerate processes you may want to shut down.")]
    public static List<object> Execute()
    {
        return ProcessDiscovery.FindAllMcpProcesses()
            .Select(p => (object)new
            {
                pid = p.Pid,
                name = p.Name,
                isCurrent = p.IsCurrent,
                commandLine = p.Extra,
            })
            .ToList();
    }
}

/// <summary>
/// Stops a specific MCP instance by PID. If the PID is the current instance, performs a graceful self-shutdown.
/// </summary>
[McpServerToolType]
public class StopInstanceTool
{
    [McpServerTool(Name = "stop_instance", Title = "Stop MCP Instance by PID",
        ReadOnly = false, Idempotent = false)]
    [Description("Stops a specific binlog-insights-mcp instance by PID. " +
                 "If the PID matches the current instance, gracefully shuts this server down. " +
                 "Combine with list_mcp_instances to enumerate PIDs first.")]
    public static string Execute(
        [Description("Process ID of the MCP instance to stop. Use list_mcp_instances to discover PIDs.")] int pid)
    {
        if (pid == ProcessDiscovery.CurrentPid)
        {
            // Graceful self-shutdown: schedule the stop so the response is flushed to the client first.
            _ = ShutdownHelper.ShutdownAfterDelayAsync();
            return $"Stopping current instance (PID {pid}).";
        }

        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            if (!p.WaitForExit(2000))
            {
                return $"Sent kill to PID {pid} but it did not exit within 2s.";
            }
            return $"Stopped PID {pid}.";
        }
        catch (ArgumentException)
        {
            return $"No process found with PID {pid}.";
        }
        catch (Exception ex)
        {
            return $"Failed to stop PID {pid}: {ex.Message}";
        }
    }
}
