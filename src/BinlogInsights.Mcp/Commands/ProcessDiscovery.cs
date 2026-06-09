using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BinlogInsights.Mcp.Commands;

internal static class ProcessDiscovery
{
    public record McpProcess(int Pid, string Name, string? Extra, bool IsCurrent);

    public static int CurrentPid => Environment.ProcessId;

    public static List<McpProcess> FindAllMcpProcesses()
    {
        var all = FindOtherMcpProcesses();
        try
        {
            using var self = Process.GetCurrentProcess();
            all.Insert(0, new McpProcess(self.Id, self.ProcessName, self.MainModule?.FileName, IsCurrent: true));
        }
        catch
        {
            all.Insert(0, new McpProcess(Environment.ProcessId, "binlog-insights-mcp", null, IsCurrent: true));
        }
        return all;
    }

    public static List<McpProcess> FindOtherMcpProcesses()
    {
        var currentPid = Environment.ProcessId;
        var found = new List<McpProcess>();

        // Windows: match binlog-insights-mcp.exe
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var proc in Process.GetProcessesByName("binlog-insights-mcp"))
            {
                if (proc.Id != currentPid)
                    found.Add(new McpProcess(proc.Id, proc.ProcessName, proc.MainModule?.FileName, IsCurrent: false));
            }
        }

        // Cross-platform: match dotnet processes running binlog-insights-mcp.dll
        foreach (var proc in Process.GetProcessesByName("dotnet"))
        {
            if (proc.Id == currentPid) continue;
            try
            {
                var cmdLine = GetCommandLine(proc);
                if (cmdLine != null &&
                    (cmdLine.IndexOf("binlog-insights-mcp.dll", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     cmdLine.IndexOf("BinlogInsights.Mcp.dll", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    found.Add(new McpProcess(proc.Id, proc.ProcessName, cmdLine, IsCurrent: false));
                }
            }
            catch { /* Access denied or exited */ }
        }

        return found;
    }

    private static string? GetCommandLine(Process proc)
    {
        // Windows: prefer modern CIM (Microsoft.Management.Infrastructure). Fall back to legacy WMI.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var session = Microsoft.Management.Infrastructure.CimSession.Create(null);
                var query = $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {proc.Id}";
                foreach (var inst in session.QueryInstances("root/cimv2", "WQL", query))
                {
                    using (inst)
                    {
                        var cmd = inst.CimInstanceProperties["CommandLine"]?.Value?.ToString();
                        if (!string.IsNullOrEmpty(cmd)) return cmd;
                    }
                }
            }
            catch { /* fall through */ }

            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {proc.Id}");
                foreach (var obj in searcher.Get())
                {
                    return obj["CommandLine"]?.ToString();
                }
            }
            catch { }
        }
        // TODO: Linux/macOS: /proc/<pid>/cmdline
        return null;
    }
}
