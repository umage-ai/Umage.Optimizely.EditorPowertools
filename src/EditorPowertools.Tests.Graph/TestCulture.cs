using System.Globalization;
using System.Runtime.CompilerServices;

namespace EditorPowertools.Tests.Graph;

/// <summary>
/// Runs every test under a real culture. A Linux CI runner without LANG uses the invariant
/// culture, where localization lookups keyed on CurrentUICulture miss their strings.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
    }
}
