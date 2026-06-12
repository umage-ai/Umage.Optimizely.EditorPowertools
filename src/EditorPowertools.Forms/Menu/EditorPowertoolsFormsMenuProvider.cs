using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;
using EPiServer.Shell;
using EPiServer.Shell.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UmageAI.Optimizely.EditorPowerTools.Forms.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Forms.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Menu;

/// <summary>
/// Hangs the Forms tools off the existing Editor Powertools menu so admins find
/// them next to the rest of the toolset.
/// </summary>
[MenuProvider]
public class EditorPowertoolsFormsMenuProvider : IMenuProvider
{
    private static readonly string BaseMenuPath = MenuPaths.Global + "/cms/editorpowertools";
    private readonly LocalizationService _localization;

    public EditorPowertoolsFormsMenuProvider()
    {
        _localization = ServiceLocator.Current.GetInstance<LocalizationService>();
    }

    private string L(string path, string fallback) =>
        _localization.GetStringByCulture(path, fallback, System.Globalization.CultureInfo.CurrentUICulture);

    public IEnumerable<MenuItem> GetMenuItems()
    {
        yield return new UrlMenuItem(
            L("/editorpowertools/menu/formsoverview", "Forms Overview"),
            BaseMenuPath + "/formsoverview",
            GetResourcePath("EditorPowertoolsForms/FormsOverview"))
        {
            SortIndex = 360,
            IsAvailable = ctx => IsFeatureEnabled(ctx, nameof(FormsFeatureToggles.FormsOverview))
        };

        yield return new UrlMenuItem(
            L("/editorpowertools/menu/submissionstimeline", "Submissions Timeline"),
            BaseMenuPath + "/submissionstimeline",
            GetResourcePath("EditorPowertoolsForms/SubmissionsTimeline"))
        {
            SortIndex = 365,
            IsAvailable = ctx => IsFeatureEnabled(ctx, nameof(FormsFeatureToggles.SubmissionsTimeline))
        };
    }

    private static string GetResourcePath(string resourcePath)
    {
        return Paths.ToResource(typeof(EditorPowertoolsFormsMenuProvider), resourcePath);
    }

    private static bool IsFeatureEnabled(HttpContext context, string featureName)
    {
        var checker = context.RequestServices.GetService<FormsFeatureAccessChecker>();
        return checker?.IsFeatureEnabled(featureName) ?? true;
    }
}
