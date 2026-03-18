using System.Reflection;

namespace BinlogInsights.Mcp;

internal static class DeploymentUtilities
{
    /// <summary>
    /// Returns the informational version set by Nerdbank.GitVersioning (e.g. "0.3.42-alpha+a1b2c3d").
    /// </summary>
    public static string GetVersion()
        => Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";
}
