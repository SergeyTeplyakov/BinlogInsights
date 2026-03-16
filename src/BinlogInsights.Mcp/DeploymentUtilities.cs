using System.Reflection;

namespace BinlogInsights.Mcp;

internal static class DeploymentUtilities
{
    /// <summary>
    /// Returns the package version without hardcoding it.
    /// The version is defined in Directory.Build.props and propagated to the assembly version at build time.
    /// </summary>
    public static string GetVersion()
        => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1";
}
