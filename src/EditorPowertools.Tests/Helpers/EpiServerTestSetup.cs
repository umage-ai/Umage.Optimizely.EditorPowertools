using EPiServer.ServiceLocation;
using EPiServer.Shell.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace EditorPowertools.Tests.Helpers;

/// <summary>
/// Sets up a minimal EPiServer ServiceLocator so that static helpers
/// like Paths.ToResource work in unit tests (they return empty strings
/// but don't throw).
/// </summary>
public static class EpiServerTestSetup
{
    private static bool _initialized;
    private static readonly object Lock = new();

    public static void EnsureInitialized()
    {
        if (_initialized) return;

        lock (Lock)
        {
            if (_initialized) return;

            var services = new ServiceCollection();
            services.AddSingleton(new ModuleTable());
            var provider = services.BuildServiceProvider();

            ServiceLocator.SetServiceProvider(provider);
            _initialized = true;
        }
    }
}
