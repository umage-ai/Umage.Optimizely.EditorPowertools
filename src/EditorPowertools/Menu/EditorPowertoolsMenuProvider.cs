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

        yield return new UrlMenuItem("Bulk Property Editor", BaseMenuPath + "/bulkpropertyeditor",
            GetResourcePath("EditorPowertools/BulkPropertyEditor"))
        {
            SortIndex = 600,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.BulkPropertyEditor))
        };

        yield return new UrlMenuItem("Scheduled Jobs Gantt", BaseMenuPath + "/scheduledjobsgantt",
            GetResourcePath("EditorPowertools/ScheduledJobsGantt"))
        {
            SortIndex = 700,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ScheduledJobsGantt))
        };

        yield return new UrlMenuItem("Activity Timeline", BaseMenuPath + "/activitytimeline",
            GetResourcePath("EditorPowertools/ActivityTimeline"))
        {
            SortIndex = 800,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ActivityTimeline))
        };

        yield return new UrlMenuItem("Content Importer", BaseMenuPath + "/contentimporter",
            GetResourcePath("EditorPowertools/ContentImporter"))
        {
            SortIndex = 850,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentImporter))
        };

        yield return new UrlMenuItem("Link Checker", BaseMenuPath + "/linkchecker",
            GetResourcePath("EditorPowertools/LinkChecker"))
        {
            SortIndex = 900,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.BrokenLinkChecker))
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
