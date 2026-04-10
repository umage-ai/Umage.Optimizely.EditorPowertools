using UmageAI.Optimizely.EditorPowerTools.Permissions;
using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;
using EPiServer.Shell;
using EPiServer.Shell.Navigation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace UmageAI.Optimizely.EditorPowerTools.Menu;

[MenuProvider]
public class EditorPowertoolsMenuProvider : IMenuProvider
{
    private static readonly string BaseMenuPath = MenuPaths.Global + "/cms/editorpowertools";
    private readonly LocalizationService _localization;

    public EditorPowertoolsMenuProvider()
    {
        _localization = ServiceLocator.Current.GetInstance<LocalizationService>();
    }

    private string L(string path, string fallback) =>
        _localization.GetStringByCulture(path, fallback, System.Globalization.CultureInfo.CurrentUICulture);

    public IEnumerable<MenuItem> GetMenuItems()
    {
        yield return new SectionMenuItem(L("/editorpowertools/menu/title", "Editor Powertools"), BaseMenuPath)
        {
            Url = GetResourcePath("UmageAI.Optimizely.EditorPowerTools/Overview"),
            SortIndex = 500,
            IsAvailable = _ => true
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/overview", "Overview"), BaseMenuPath + "/overview",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/Overview"))
        {
            SortIndex = 100,
            IsAvailable = _ => true
        };

        // ── Content & Editorial ──────────────────────────────────

        yield return new UrlMenuItem(L("/editorpowertools/menu/contentstatistics", "Content Statistics"), BaseMenuPath + "/contentstatistics",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ContentStatistics"))
        {
            SortIndex = 200,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentStatistics))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/activitytimeline", "Activity Timeline"), BaseMenuPath + "/activitytimeline",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ActivityTimeline"))
        {
            SortIndex = 210,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ActivityTimeline))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/bulkpropertyeditor", "Bulk Property Editor"), BaseMenuPath + "/bulkpropertyeditor",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/BulkPropertyEditor"))
        {
            SortIndex = 220,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.BulkPropertyEditor))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/contentimporter", "Content Importer"), BaseMenuPath + "/contentimporter",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ContentImporter"))
        {
            SortIndex = 230,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentImporter))
        };

        // ── Audits & Analysis ────────────────────────────────────

        yield return new UrlMenuItem(L("/editorpowertools/menu/contentaudit", "Content Audit"), BaseMenuPath + "/contentaudit",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ContentAudit"))
        {
            SortIndex = 300,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentAudit))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/contenttypeaudit", "Content Type Audit"), BaseMenuPath + "/contenttypeaudit",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ContentTypeAudit"))
        {
            SortIndex = 310,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeAudit))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/personalizationaudit", "Personalization Audit"), BaseMenuPath + "/personalizationaudit",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/PersonalizationAudit"))
        {
            SortIndex = 320,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.PersonalizationUsageAudit))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/languageaudit", "Language Audit"), BaseMenuPath + "/languageaudit",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/LanguageAudit"))
        {
            SortIndex = 330,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.LanguageAudit))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/securityaudit", "Security Audit"), BaseMenuPath + "/securityaudit",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/SecurityAudit"))
        {
            SortIndex = 340,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.SecurityAudit))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/linkaudit", "Link Audit"), BaseMenuPath + "/linkchecker",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/LinkChecker"))
        {
            SortIndex = 350,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.BrokenLinkChecker))
        };

        // ── Configuration & Admin ────────────────────────────────

        yield return new UrlMenuItem(L("/editorpowertools/menu/contenttyperecommendations", "Content Type Recommendations"), BaseMenuPath + "/contenttyperecommendations",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ContentTypeRecommendations"))
        {
            SortIndex = 400,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ContentTypeRecommendations))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/audiencemanager", "Audience Manager"), BaseMenuPath + "/audiencemanager",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/AudienceManager"))
        {
            SortIndex = 410,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.AudienceManager))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/cmsdoctor", "CMS Doctor"), BaseMenuPath + "/cmsdoctor",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/CmsDoctor"))
        {
            SortIndex = 420,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.CmsDoctor))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/activeeditors", "Active Editors"), BaseMenuPath + "/activeeditors",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ActiveEditors"))
        {
            SortIndex = 430,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ActiveEditors))
        };

        yield return new UrlMenuItem(L("/editorpowertools/menu/scheduledjobsgantt", "Scheduled Jobs Gantt"), BaseMenuPath + "/scheduledjobsgantt",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/ScheduledJobsGantt"))
        {
            SortIndex = 440,
            IsAvailable = context => IsFeatureEnabled(context, nameof(Configuration.FeatureToggles.ScheduledJobsGantt))
        };

        // ── About ────────────────────────────────────────────────

        yield return new UrlMenuItem(L("/editorpowertools/menu/about", "About"), BaseMenuPath + "/about",
            GetResourcePath("UmageAI.Optimizely.EditorPowerTools/About"))
        {
            SortIndex = 900,
            IsAvailable = _ => true
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
