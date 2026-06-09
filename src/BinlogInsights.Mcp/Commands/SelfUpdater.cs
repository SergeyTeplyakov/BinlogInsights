using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BinlogInsights.Mcp.Commands;

/// <summary>
/// Self-update support: checks nuget.org for a newer release of this tool and,
/// when asked, launches a detached helper that stops all running instances and
/// runs <c>dotnet tool update -g</c>. The current process can't replace its own
/// locked shim on Windows, so the actual install happens out-of-process.
/// </summary>
internal static class SelfUpdater
{
    /// <summary>NuGet package id (case-insensitive; lower-cased for the flat-container API).</summary>
    public const string PackageId = "BinlogInsights.Mcp";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public record UpdateInfo(string CurrentVersion, string? LatestVersion, bool UpdateAvailable, string? Error);

    /// <summary>
    /// Queries nuget.org for the latest stable version and compares it with the
    /// running build. Tries the flat-container index first, then falls back to the
    /// search endpoint if the primary host is unreachable.
    /// </summary>
    public static async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        string current = DeploymentUtilities.GetVersion();
        var currentCore = ParseCore(current);

        string? latest;
        try
        {
            latest = await GetLatestStableAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new UpdateInfo(current, null, false, $"Failed to query nuget.org: {ex.Message}");
        }

        if (latest is null)
        {
            return new UpdateInfo(current, null, false, "No stable versions found on nuget.org.");
        }

        var latestCore = ParseCore(latest);
        bool available = currentCore is not null && latestCore is not null && latestCore > currentCore;
        return new UpdateInfo(current, latest, available, null);
    }

    /// <summary>
    /// Writes and launches a detached updater script that stops every running
    /// MCP instance (so the global-tool shim unlocks) and installs <paramref name="targetVersion"/>.
    /// Returns the path to the log file the script writes for diagnostics.
    /// </summary>
    public static string LaunchDetachedUpdater(string targetVersion)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string dir = Path.GetTempPath();
        string logPath = Path.Combine(dir, $"binloginsights-selfupdate-{stamp}.log");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string scriptPath = Path.Combine(dir, $"binloginsights-selfupdate-{stamp}.ps1");
            File.WriteAllText(scriptPath, BuildPowerShellScript()
                .Replace("__LOG__", logPath)
                .Replace("__PKG__", PackageId)
                .Replace("__VER__", targetVersion));

            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            try
            {
                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                psi.FileName = "powershell";
                Process.Start(psi);
            }
        }
        else
        {
            string scriptPath = Path.Combine(dir, $"binloginsights-selfupdate-{stamp}.sh");
            File.WriteAllText(scriptPath, BuildBashScript()
                .Replace("__LOG__", logPath)
                .Replace("__PKG__", PackageId)
                .Replace("__VER__", targetVersion));

            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }

        return logPath;
    }

    private static async Task<string?> GetLatestStableAsync(CancellationToken cancellationToken)
    {
        // Primary: NuGet V3 flat-container version index.
        try
        {
            string url = $"https://api.nuget.org/v3-flatcontainer/{PackageId.ToLowerInvariant()}/index.json";
            var doc = await Http.GetFromJsonAsync<FlatContainerIndex>(url, cancellationToken).ConfigureAwait(false);
            string? latest = PickLatestStable(doc?.Versions);
            if (latest is not null)
            {
                return latest;
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Fall through to the search endpoint.
        }

        // Fallback: NuGet search service (different host, manual JSON read).
        string searchUrl =
            $"https://azuresearch-usnc.nuget.org/query?q=packageid:{PackageId}&prerelease=false&semVerLevel=2.0.0";
        using var resp = await Http.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (json.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
        {
            var first = data[0];
            if (first.TryGetProperty("version", out var v))
            {
                return v.GetString();
            }
        }

        return null;
    }

    private static string? PickLatestStable(IEnumerable<string>? versions)
    {
        if (versions is null)
        {
            return null;
        }

        string? best = null;
        Version? bestCore = null;
        foreach (var raw in versions)
        {
            // Skip prerelease versions (they contain a '-' before any build metadata).
            string trimmed = raw.Trim();
            int plus = trimmed.IndexOf('+');
            string noMeta = plus >= 0 ? trimmed[..plus] : trimmed;
            if (noMeta.Contains('-'))
            {
                continue;
            }

            var core = ParseCore(raw);
            if (core is not null && (bestCore is null || core > bestCore))
            {
                bestCore = core;
                best = raw;
            }
        }

        return best;
    }

    /// <summary>
    /// Extracts the comparable Major.Minor.Patch core from a version string,
    /// dropping any 4th component (git height) and prerelease/build metadata.
    /// </summary>
    private static Version? ParseCore(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string s = raw.Trim();
        int plus = s.IndexOf('+');
        if (plus >= 0)
        {
            s = s[..plus];
        }

        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            s = s[..dash];
        }

        var parts = s.Split('.');
        int major = 0, minor = 0, patch = 0;
        if (parts.Length > 0)
        {
            int.TryParse(parts[0], out major);
        }
        if (parts.Length > 1)
        {
            int.TryParse(parts[1], out minor);
        }
        if (parts.Length > 2)
        {
            int.TryParse(parts[2], out patch);
        }

        return new Version(major, minor, patch);
    }

    private static string BuildPowerShellScript() => """
        $ErrorActionPreference = 'SilentlyContinue'
        $log = '__LOG__'
        $pkg = '__PKG__'
        $ver = '__VER__'
        function Log($m) { "[{0}] {1}" -f (Get-Date -Format o), $m | Out-File -FilePath $log -Append -Encoding utf8 }
        Log "Self-update requested -> $ver"
        # Let the requesting server flush its response and shut down gracefully.
        Start-Sleep -Seconds 2
        for ($i = 0; $i -lt 30; $i++) {
            $procs = @(Get-Process -Name 'binlog-insights-mcp' -ErrorAction SilentlyContinue)
            $dn = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue |
                Where-Object { $_.CommandLine -match 'BinlogInsights\.Mcp\.dll|binlog-insights-mcp\.dll' })
            if ($procs.Count -eq 0 -and $dn.Count -eq 0) { break }
            foreach ($p in $procs) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
            foreach ($d in $dn) { Stop-Process -Id $d.ProcessId -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Milliseconds 500
        }
        Log "Instances stopped; running 'dotnet tool update -g $pkg --version $ver'"
        & dotnet tool update -g $pkg --version $ver *>> $log
        Log "dotnet tool update exit code: $LASTEXITCODE"
        """;

    private static string BuildBashScript() => """
        #!/usr/bin/env bash
        log="__LOG__"
        pkg="__PKG__"
        ver="__VER__"
        echo "$(date -Is) self-update requested -> $ver" >> "$log"
        sleep 2
        for i in $(seq 1 30); do
            pids=$(pgrep -f 'BinlogInsights\.Mcp\.dll|binlog-insights-mcp' || true)
            if [ -z "$pids" ]; then break; fi
            echo "$pids" | xargs -r kill -9 2>/dev/null || true
            sleep 0.5
        done
        echo "$(date -Is) instances stopped; running dotnet tool update" >> "$log"
        dotnet tool update -g "$pkg" --version "$ver" >> "$log" 2>&1
        echo "$(date -Is) dotnet tool update exit $?" >> "$log"
        """;

    private sealed record FlatContainerIndex(IReadOnlyList<string>? Versions);
}
