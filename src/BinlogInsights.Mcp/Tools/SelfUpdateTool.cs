using System.ComponentModel;
using BinlogInsights.Mcp.Commands;
using ModelContextProtocol.Server;

namespace BinlogInsights.Mcp;

/// <summary>
/// Self-update tool: checks nuget.org for a newer release and, when explicitly
/// asked, launches a detached helper that stops all running instances and runs
/// <c>dotnet tool update -g</c>.
/// </summary>
[McpServerToolType]
public class SelfUpdateTool
{
    [McpServerTool(Name = "self_update", Title = "Check For / Apply Tool Update",
        ReadOnly = false, Idempotent = false)]
    [Description(
        "Checks a NuGet feed for a newer release of binlog-insights-mcp and optionally upgrades the globally installed tool. " +
        "By default this is a safe dry-run: it reports the current and latest versions without changing anything. " +
        "Pass apply=true to perform the upgrade — this stops ALL running binlog-insights-mcp instances (including this one) " +
        "so the global-tool shim unlocks, then runs 'dotnet tool update -g BinlogInsights.Mcp' in a detached background process. " +
        "Use 'source' to point at a custom feed (e.g. a local folder for testing) and allow_prerelease to include prerelease builds. " +
        "After applying, restart the MCP server in your client to load the new version.")]
    public static async Task<string> Execute(
        [Description("When true, performs the upgrade if a newer version exists. When false (default), only reports availability.")]
        bool apply = false,
        [Description("Optional NuGet feed: a local folder path or feed URL. Defaults to nuget.org when omitted. Use a local folder to test self-update.")]
        string? source = null,
        [Description("When true, prerelease versions are considered. Useful with a local test feed. Default false.")]
        bool allow_prerelease = false,
        CancellationToken cancellationToken = default)
    {
        var info = await SelfUpdater.CheckForUpdateAsync(source, allow_prerelease, cancellationToken).ConfigureAwait(false);

        if (info.Error is not null)
        {
            return $"Update check failed: {info.Error}\nCurrent version: {info.CurrentVersion}";
        }

        if (!info.UpdateAvailable)
        {
            return $"Already up to date.\nCurrent version: {info.CurrentVersion}\n" +
                   $"Latest on {info.SourceDescription}: {info.LatestVersion}";
        }

        if (!apply)
        {
            return $"Update available on {info.SourceDescription}: {info.CurrentVersion} -> {info.LatestVersion}\n" +
                   "Run self_update with apply=true to upgrade. This will stop all running instances " +
                   "(including this server) and install the new version in the background.";
        }

        string logPath = SelfUpdater.LaunchDetachedUpdater(info.LatestVersion!, source);

        // Gracefully shut this instance down so its shim unlocks; the detached
        // updater also force-stops any instances that linger.
        _ = ShutdownHelper.ShutdownAfterDelayAsync();

        return $"Upgrading {info.CurrentVersion} -> {info.LatestVersion} (from {info.SourceDescription}) in the background.\n" +
               "All running binlog-insights-mcp instances (including this one) are being stopped, then " +
               "'dotnet tool update -g' will install the new version.\n" +
               $"Progress log: {logPath}\n" +
               "Restart the MCP server in your client once the update completes.";
    }
}
