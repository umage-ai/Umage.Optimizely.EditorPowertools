using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Helpers;

/// <summary>
/// Sets up a minimal EPiServer <see cref="ServiceLocator"/> so the CMS Doctor
/// checks can run in isolation. <see cref="DoctorCheckBase"/> resolves a
/// <see cref="LocalizationService"/> from <c>ServiceLocator.Current</c> when it
/// builds a result (for the check Name / Group and the localized messages), so a
/// concrete service must be registered. An empty <see cref="MemoryLocalizationService"/>
/// has no strings, which means every <c>GetStringByCulture(path, fallback, ...)</c>
/// call returns the supplied English fallback — exactly what the assertions expect.
/// </summary>
public static class FormsTestSetup
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
            services.AddLogging();
            services.AddSingleton<LocalizationService, MemoryLocalizationService>();

            var provider = services.BuildServiceProvider();
            ServiceLocator.SetServiceProvider(provider);
            _initialized = true;
        }
    }
}
