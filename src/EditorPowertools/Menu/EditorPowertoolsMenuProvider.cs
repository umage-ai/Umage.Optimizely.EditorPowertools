using EditorPowertools.Permissions;
using EPiServer.Shell;
using EPiServer.Shell.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EditorPowertools.Menu;

[MenuProvider]
public class EditorPowertoolsMenuProvider : IMenuProvider
{
    private static readonly string BaseMenuPath = MenuPaths.Global + "/cms/editorpowertools";

    public IEnumerable<MenuItem> GetMenuItems()
    {
        yield return new SectionMenuItem("Editor Powertools", BaseMenuPath)
        {
            Url = GetResourcePath("EditorPowertools/Overview"),
            SortIndex = 500,
            IsAvailable = _ => true
        };

        yield return new UrlMenuItem("Overview", BaseMenuPath + "/overview",
            GetResourcePath("EditorPowertools/Overview"))
        {
            SortIndex = 100,
            IsAvailable = _ => true
        };

        yield return new UrlMenuItem("Content Type Audit", BaseMenuPath + "/contenttypeaudit",
            GetResourcePath("EditorPowertools/ContentTypeAudit"))
        {
            SortIndex = 200,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeAudit))
        };

        yield return new UrlMenuItem("Personalization Audit", BaseMenuPath + "/personalizationaudit",
            GetResourcePath("EditorPowertools/PersonalizationAudit"))
        {
            SortIndex = 300,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.PersonalizationUsageAudit))
        };

        yield return new UrlMenuItem("Audience Manager", BaseMenuPath + "/audiencemanager",
            GetResourcePath("EditorPowertools/AudienceManager"))
        {
            SortIndex = 400,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.AudienceManager))
        };

        yield return new UrlMenuItem("Content Type Recommendations", BaseMenuPath + "/contenttyperecommendations",
            GetResourcePath("EditorPowertools/ContentTypeRecommendations"))
        {
            SortIndex = 500,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeRecommendations))
        };
    }

    private static string GetResourcePath(string resourcePath)
    {
        return Paths.ToResource(typeof(EditorPowertoolsMenuProvider), resourcePath);
    }

    private static bool IsFeatureEnabled(HttpContext context, string featureName)
    {
        var checker = context.RequestServices.GetService<FeatureAccessChecker>();
        return checker?.IsFeatureEnabled(featureName) ?? true;
    }
}
