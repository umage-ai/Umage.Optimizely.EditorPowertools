using EditorPowertools.Permissions;
using EPiServer.Shell.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EditorPowertools.Menu;

[MenuProvider]
public class EditorPowertoolsMenuProvider : IMenuProvider
{
    public IEnumerable<MenuItem> GetMenuItems()
    {
        var section = new SectionMenuItem("Editor Powertools", MenuPaths.Global + "/cms/editorpowertools")
        {
            SortIndex = 500,
            IsAvailable = _ => true
        };

        var overview = new UrlMenuItem("Overview", MenuPaths.Global + "/cms/editorpowertools/overview",
            "/editorpowertools")
        {
            SortIndex = 100,
            IsAvailable = _ => true
        };

        var contentTypeAudit = new UrlMenuItem("Content Type Audit", MenuPaths.Global + "/cms/editorpowertools/contenttypeaudit",
            "/editorpowertools/content-type-audit")
        {
            SortIndex = 200,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeAudit))
        };

        var personalizationAudit = new UrlMenuItem("Personalization Audit", MenuPaths.Global + "/cms/editorpowertools/personalizationaudit",
            "/editorpowertools/personalization-audit")
        {
            SortIndex = 300,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.PersonalizationUsageAudit))
        };

        var audienceManager = new UrlMenuItem("Audience Manager", MenuPaths.Global + "/cms/editorpowertools/audiencemanager",
            "/editorpowertools/audience-manager")
        {
            SortIndex = 400,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.AudienceManager))
        };

        var contentTypeRecommendations = new UrlMenuItem("Content Type Recommendations", MenuPaths.Global + "/cms/editorpowertools/contenttyperecommendations",
            "/editorpowertools/content-type-recommendations")
        {
            SortIndex = 500,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeRecommendations))
        };

        return new MenuItem[]
        {
            section,
            overview,
            contentTypeAudit,
            personalizationAudit,
            audienceManager,
            contentTypeRecommendations
        };
    }

    private static bool IsFeatureEnabled(HttpContext context, string featureName)
    {
        var checker = context.RequestServices.GetService<FeatureAccessChecker>();
        return checker?.IsFeatureEnabled(featureName) ?? true;
    }
}
