using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;
using EPiServer.Shell;
using EPiServer.Shell.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Menu;

/// <summary>
/// Hangs the Graph tools off the existing Editor Powertools menu so admins find
/// them next to the rest of the toolset.
/// </summary>
[MenuProvider]
public class EditorPowertoolsGraphMenuProvider : IMenuProvider
{
    private static readonly string BaseMenuPath = MenuPaths.Global + "/cms/editorpowertools";
    private readonly LocalizationService _localization;

    public EditorPowertoolsGraphMenuProvider()
    {
        _localization = ServiceLocator.Current.GetInstance<LocalizationService>();
    }

    private string L(string path, string fallback) =>
        _localization.GetStringByCulture(path, fallback, System.Globalization.CultureInfo.CurrentUICulture);

    public IEnumerable<MenuItem> GetMenuItems()
    {
        yield return new UrlMenuItem(
            L("/editorpowertools/menu/powersearch", "PowerSearch"),
            BaseMenuPath + "/powersearch",
            GetResourcePath("EditorPowertoolsGraph/PowerSearch"))
        {
            SortIndex = 370,
            IsAvailable = ctx => IsFeatureEnabled(ctx, nameof(GraphFeatureToggles.PowerSearch))
        };
    }

    private static string GetResourcePath(string resourcePath)
    {
        return Paths.ToResource(typeof(EditorPowertoolsGraphMenuProvider), resourcePath);
    }

    private static bool IsFeatureEnabled(HttpContext context, string featureName)
    {
        var checker = context.RequestServices.GetService<GraphFeatureAccessChecker>();
        return checker?.IsFeatureEnabled(featureName) ?? true;
    }
}
