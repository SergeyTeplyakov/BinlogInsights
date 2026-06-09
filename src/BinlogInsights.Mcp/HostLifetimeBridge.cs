using Microsoft.Extensions.Hosting;

namespace BinlogInsights.Mcp;

internal static class HostLifetimeBridge
{
    private static IHostApplicationLifetime? s_lifetime;

    public static void Initialize(IHostApplicationLifetime lifetime)
    {
        s_lifetime = lifetime;
    }

    public static bool TryStop()
    {
        var lifetime = s_lifetime;
        if (lifetime is null)
        {
            return false;
        }

        lifetime.StopApplication();
        return true;
    }
}
