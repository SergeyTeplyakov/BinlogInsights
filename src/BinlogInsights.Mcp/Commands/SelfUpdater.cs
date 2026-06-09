using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BinlogInsights.Mcp.Commands;

/// <summary>
/// Self-update support: checks a NuGet feed (nuget.org by default, or a custom
/// source such as a local folder for testing) for a newer release of this tool
/// and, when asked, launches a detached helper that stops all running instances
/// and runs <c>dotnet tool update -g</c>. The current process can't replace its
/// own locked shim on Windows, so the actual install happens out-of-process.
/// </summary>
internal static class SelfUpdater
{
    /// <summary>NuGet package id (case-insensitive; lower-cased for the flat-container API).</summary>
    public const string PackageId = "BinlogInsights.Mcp";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public record UpdateInfo(
        string CurrentVersion,
        string? LatestVersion,
        bool UpdateAvailable,
        string? Error,
        string SourceDescription);

    /// <summary>
    /// Finds the latest candidate version on the given <paramref name="source"/>
    /// (a local directory or feed URL; nuget.org when null/empty) and compares it
    /// with the running build using SemVer ordering. Tries the flat-container
    /// index first for nuget.org, then falls back to the search endpoint.
    /// </summary>
    public static async Task<UpdateInfo> CheckForUpdateAsync(
        string? source,
        bool allowPrerelease,
        CancellationToken cancellationToken)
    {
        string current = DeploymentUtilities.GetVersion();
        bool isLocalDir = !string.IsNullOrWhiteSpace(source) && Directory.Exists(source);
        string sourceDescription = string.IsNullOrWhiteSpace(source)
            ? "nuget.org"
            : (isLocalDir ? $"local feed '{source}'" : source!);

        string? latest;
        try
        {
            latest = isLocalDir
                ? GetLatestFromLocalFeed(source!, allowPrerelease)
                : await GetLatestStableAsync(allowPrerelease, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new UpdateInfo(current, null, false, $"Failed to query {sourceDescription}: {ex.Message}", sourceDescription);
        }

        if (latest is null)
        {
            return new UpdateInfo(current, null, false, $"No matching versions found on {sourceDescription}.", sourceDescription);
        }

        bool available = CompareSemver(latest, current) > 0;
        return new UpdateInfo(current, latest, available, null, sourceDescription);
    }

    /// <summary>
    /// Writes and launches a detached updater script that stops every running
    /// MCP instance (so the global-tool shim unlocks) and installs
    /// <paramref name="targetVersion"/>, adding <paramref name="source"/> as an
    /// extra feed when provided. Returns the log file path for diagnostics.
    /// </summary>
    public static string LaunchDetachedUpdater(string targetVersion, string? source)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string dir = Path.GetTempPath();
        string logPath = Path.Combine(dir, $"binloginsights-selfupdate-{stamp}.log");

        // Extra feed (quoted) when a custom source is given; empty otherwise.
        string sourceArg = string.IsNullOrWhiteSpace(source) ? string.Empty : $"--add-source \"{source}\"";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string scriptPath = Path.Combine(dir, $"binloginsights-selfupdate-{stamp}.ps1");
            File.WriteAllText(scriptPath, BuildPowerShellScript()
                .Replace("__LOG__", logPath)
                .Replace("__PKG__", PackageId)
                .Replace("__VER__", targetVersion)
                .Replace("__SRC__", sourceArg));

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
                .Replace("__VER__", targetVersion)
                .Replace("__SRC__", sourceArg));

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

    /// <summary>
    /// Scans a local folder feed for <c>{PackageId}.{version}.nupkg</c> files and
    /// returns the highest version (respecting <paramref name="allowPrerelease"/>).
    /// </summary>
    private static string? GetLatestFromLocalFeed(string dir, bool allowPrerelease)
    {
        string prefix = PackageId + ".";
        string? best = null;
        foreach (var path in Directory.EnumerateFiles(dir, $"{PackageId}.*.nupkg", SearchOption.AllDirectories))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string version = name[prefix.Length..];
            if (!allowPrerelease && IsPrerelease(version))
            {
                continue;
            }

            if (best is null || CompareSemver(version, best) > 0)
            {
                best = version;
            }
        }

        return best;
    }

    private static async Task<string?> GetLatestStableAsync(bool allowPrerelease, CancellationToken cancellationToken)
    {
        // Primary: NuGet V3 flat-container version index.
        try
        {
            string url = $"https://api.nuget.org/v3-flatcontainer/{PackageId.ToLowerInvariant()}/index.json";
            var doc = await Http.GetFromJsonAsync<FlatContainerIndex>(url, cancellationToken).ConfigureAwait(false);
            string? latest = PickLatest(doc?.Versions, allowPrerelease);
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
            $"https://azuresearch-usnc.nuget.org/query?q=packageid:{PackageId}&prerelease={(allowPrerelease ? "true" : "false")}&semVerLevel=2.0.0";
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

    private static string? PickLatest(IEnumerable<string>? versions, bool allowPrerelease)
    {
        if (versions is null)
        {
            return null;
        }

        string? best = null;
        foreach (var raw in versions)
        {
            string trimmed = raw.Trim();
            if (!allowPrerelease && IsPrerelease(trimmed))
            {
                continue;
            }

            if (best is null || CompareSemver(trimmed, best) > 0)
            {
                best = trimmed;
            }
        }

        return best;
    }

    /// <summary>True when the version carries a prerelease tag (a '-' before any build metadata).</summary>
    private static bool IsPrerelease(string version)
    {
        int plus = version.IndexOf('+');
        string noMeta = plus >= 0 ? version[..plus] : version;
        return noMeta.Contains('-');
    }

    /// <summary>
    /// SemVer-style comparison: numeric Major.Minor.Patch first, then a release
    /// outranks a prerelease, then dot-separated prerelease identifiers (numeric
    /// compared numerically, otherwise ordinal). Build metadata is ignored.
    /// </summary>
    private static int CompareSemver(string a, string b)
    {
        var (coreA, preA) = SplitVersion(a);
        var (coreB, preB) = SplitVersion(b);

        int byCore = coreA.CompareTo(coreB);
        if (byCore != 0)
        {
            return byCore;
        }

        bool aPre = preA.Length > 0;
        bool bPre = preB.Length > 0;
        if (aPre && !bPre)
        {
            return -1;
        }
        if (!aPre && bPre)
        {
            return 1;
        }
        if (!aPre && !bPre)
        {
            return 0;
        }

        return ComparePrerelease(preA, preB);
    }

    private static int ComparePrerelease(string a, string b)
    {
        string[] ai = a.Split('.');
        string[] bi = b.Split('.');
        int n = Math.Max(ai.Length, bi.Length);
        for (int i = 0; i < n; i++)
        {
            if (i >= ai.Length)
            {
                return -1; // fewer identifiers => lower precedence
            }
            if (i >= bi.Length)
            {
                return 1;
            }

            string x = ai[i], y = bi[i];
            bool xNum = int.TryParse(x, out int xi);
            bool yNum = int.TryParse(y, out int yi);
            int c = (xNum, yNum) switch
            {
                (true, true) => xi.CompareTo(yi),
                (true, false) => -1,   // numeric identifiers rank below alphanumeric
                (false, true) => 1,
                _ => string.CompareOrdinal(x, y),
            };
            if (c != 0)
            {
                return c;
            }
        }

        return 0;
    }

    /// <summary>
    /// Splits a version into its comparable numeric core (Major.Minor.Patch,
    /// dropping any 4th git-height component) and its prerelease label.
    /// </summary>
    private static (Version Core, string Prerelease) SplitVersion(string raw)
    {
        string s = (raw ?? string.Empty).Trim();

        int plus = s.IndexOf('+');
        if (plus >= 0)
        {
            s = s[..plus];
        }

        string prerelease = string.Empty;
        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = s[(dash + 1)..];
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

        return (new Version(major, minor, patch), prerelease);
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
        Log "Instances stopped; running 'dotnet tool update -g $pkg --version $ver __SRC__'"
        & dotnet tool update -g $pkg --version $ver __SRC__ *>> $log
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
        dotnet tool update -g "$pkg" --version "$ver" __SRC__ >> "$log" 2>&1
        echo "$(date -Is) dotnet tool update exit $?" >> "$log"
        """;

    private sealed record FlatContainerIndex(IReadOnlyList<string>? Versions);
}
