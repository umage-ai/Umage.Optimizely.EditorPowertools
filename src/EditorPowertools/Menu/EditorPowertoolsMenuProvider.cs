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

        // ── Content & Editorial ──────────────────────────────────

        yield return new UrlMenuItem("Content Statistics", BaseMenuPath + "/contentstatistics",
            GetResourcePath("EditorPowertools/ContentStatistics"))
        {
            SortIndex = 200,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentStatistics))
        };

        yield return new UrlMenuItem("Activity Timeline", BaseMenuPath + "/activitytimeline",
            GetResourcePath("EditorPowertools/ActivityTimeline"))
        {
            SortIndex = 210,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ActivityTimeline))
        };

        yield return new UrlMenuItem("Bulk Property Editor", BaseMenuPath + "/bulkpropertyeditor",
            GetResourcePath("EditorPowertools/BulkPropertyEditor"))
        {
            SortIndex = 220,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.BulkPropertyEditor))
        };

        yield return new UrlMenuItem("Content Importer", BaseMenuPath + "/contentimporter",
            GetResourcePath("EditorPowertools/ContentImporter"))
        {
            SortIndex = 230,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentImporter))
        };

        // ── Audits & Analysis ────────────────────────────────────

        yield return new UrlMenuItem("Content Audit", BaseMenuPath + "/contentaudit",
            GetResourcePath("EditorPowertools/ContentAudit"))
        {
            SortIndex = 300,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentAudit))
        };

        yield return new UrlMenuItem("Content Type Audit", BaseMenuPath + "/contenttypeaudit",
            GetResourcePath("EditorPowertools/ContentTypeAudit"))
        {
            SortIndex = 310,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeAudit))
        };

        yield return new UrlMenuItem("Personalization Audit", BaseMenuPath + "/personalizationaudit",
            GetResourcePath("EditorPowertools/PersonalizationAudit"))
        {
            SortIndex = 320,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.PersonalizationUsageAudit))
        };

        yield return new UrlMenuItem("Language Audit", BaseMenuPath + "/languageaudit",
            GetResourcePath("EditorPowertools/LanguageAudit"))
        {
            SortIndex = 330,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.LanguageAudit))
        };

        yield return new UrlMenuItem("Security Audit", BaseMenuPath + "/securityaudit",
            GetResourcePath("EditorPowertools/SecurityAudit"))
        {
            SortIndex = 340,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.SecurityAudit))
        };

        yield return new UrlMenuItem("Link Audit", BaseMenuPath + "/linkchecker",
            GetResourcePath("EditorPowertools/LinkChecker"))
        {
            SortIndex = 350,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.BrokenLinkChecker))
        };

        // ── Configuration & Admin ────────────────────────────────

        yield return new UrlMenuItem("Content Type Recommendations", BaseMenuPath + "/contenttyperecommendations",
            GetResourcePath("EditorPowertools/ContentTypeRecommendations"))
        {
            SortIndex = 400,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeRecommendations))
        };

        yield return new UrlMenuItem("Audience Manager", BaseMenuPath + "/audiencemanager",
            GetResourcePath("EditorPowertools/AudienceManager"))
        {
            SortIndex = 410,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.AudienceManager))
        };

        yield return new UrlMenuItem("CMS Doctor", BaseMenuPath + "/cmsdoctor",
            GetResourcePath("EditorPowertools/CmsDoctor"))
        {
            SortIndex = 420,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.CmsDoctor))
        };

        yield return new UrlMenuItem("Active Editors", BaseMenuPath + "/activeeditors",
            GetResourcePath("EditorPowertools/ActiveEditors"))
        {
            SortIndex = 430,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ActiveEditors))
        };

        yield return new UrlMenuItem("Scheduled Jobs Gantt", BaseMenuPath + "/scheduledjobsgantt",
            GetResourcePath("EditorPowertools/ScheduledJobsGantt"))
        {
            SortIndex = 440,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ScheduledJobsGantt))
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
