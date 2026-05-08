using EPiServer.Shell;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using UmageAI.Optimizely.EditorPowerTools.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Menu;
using UmageAI.Optimizely.EditorPowerTools.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

/// <summary>
/// Registers descriptors for the tools shipped in the base library. Kept as
/// an internal helper so <c>AddEditorPowertools()</c> stays uncluttered.
/// </summary>
internal static class BuiltInPowertoolDescriptors
{
    public static IServiceCollection AddBuiltInPowertoolDescriptors(this IServiceCollection services)
    {
        // Resource paths are resolved per-request via IPowertoolDescriptor.Url so
        // the consuming host's module mount point is honored — never hardcoded.
        string Url(string path) => Paths.ToResource(typeof(EditorPowertoolsMenuProvider), path);
        bool Available(HttpContext ctx, string feature) =>
            ctx.RequestServices.GetService<FeatureAccessChecker>()?.IsFeatureEnabled(feature) ?? true;

        // ── Content & Editorial ────────────────────────────────────
        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ContentStatistics",
            Title = "/editorpowertools/tools/contentstatistics/title",
            Description = "/editorpowertools/tools/contentstatistics/description",
            Url = Url("EditorPowertools/ContentStatistics"),
            IconSvgPath = SvgIcons.ContentStatistics,
            Group = PowertoolGroups.ContentEditorial,
            SortIndex = 200,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ContentStatistics))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ActivityTimeline",
            Title = "/editorpowertools/tools/activitytimeline/title",
            Description = "/editorpowertools/tools/activitytimeline/description",
            Url = Url("EditorPowertools/ActivityTimeline"),
            IconSvgPath = SvgIcons.ActivityTimeline,
            Group = PowertoolGroups.ContentEditorial,
            SortIndex = 210,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ActivityTimeline))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "BulkPropertyEditor",
            Title = "/editorpowertools/tools/bulkpropertyeditor/title",
            Description = "/editorpowertools/tools/bulkpropertyeditor/description",
            Url = Url("EditorPowertools/BulkPropertyEditor"),
            IconSvgPath = SvgIcons.BulkPropertyEditor,
            Group = PowertoolGroups.ContentEditorial,
            SortIndex = 220,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.BulkPropertyEditor))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ContentImporter",
            Title = "/editorpowertools/tools/contentimporter/title",
            Description = "/editorpowertools/tools/contentimporter/description",
            Url = Url("EditorPowertools/ContentImporter"),
            IconSvgPath = SvgIcons.ContentImporter,
            Group = PowertoolGroups.ContentEditorial,
            SortIndex = 230,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ContentImporter))
        });

        // ── Audits & Analysis ──────────────────────────────────────
        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ContentAudit",
            Title = "/editorpowertools/tools/contentaudit/title",
            Description = "/editorpowertools/tools/contentaudit/description",
            Url = Url("EditorPowertools/ContentAudit"),
            IconSvgPath = SvgIcons.ContentAudit,
            Group = PowertoolGroups.AuditsAnalysis,
            SortIndex = 300,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ContentAudit))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ContentTypeAudit",
            Title = "/editorpowertools/tools/contenttypeaudit/title",
            Description = "/editorpowertools/tools/contenttypeaudit/description",
            Url = Url("EditorPowertools/ContentTypeAudit"),
            IconSvgPath = SvgIcons.ContentTypeAudit,
            Group = PowertoolGroups.AuditsAnalysis,
            SortIndex = 310,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ContentTypeAudit))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "PersonalizationAudit",
            Title = "/editorpowertools/tools/personalizationaudit/title",
            Description = "/editorpowertools/tools/personalizationaudit/description",
            Url = Url("EditorPowertools/PersonalizationAudit"),
            IconSvgPath = SvgIcons.PersonalizationAudit,
            Group = PowertoolGroups.AuditsAnalysis,
            SortIndex = 320,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.PersonalizationUsageAudit))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "LanguageAudit",
            Title = "/editorpowertools/tools/languageaudit/title",
            Description = "/editorpowertools/tools/languageaudit/description",
            Url = Url("EditorPowertools/LanguageAudit"),
            IconSvgPath = SvgIcons.LanguageAudit,
            Group = PowertoolGroups.AuditsAnalysis,
            SortIndex = 330,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.LanguageAudit))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "SecurityAudit",
            Title = "/editorpowertools/tools/securityaudit/title",
            Description = "/editorpowertools/tools/securityaudit/description",
            Url = Url("EditorPowertools/SecurityAudit"),
            IconSvgPath = SvgIcons.SecurityAudit,
            Group = PowertoolGroups.AuditsAnalysis,
            SortIndex = 340,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.SecurityAudit))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "LinkChecker",
            Title = "/editorpowertools/tools/linkaudit/title",
            Description = "/editorpowertools/tools/linkaudit/description",
            Url = Url("EditorPowertools/LinkChecker"),
            IconSvgPath = SvgIcons.LinkChecker,
            Group = PowertoolGroups.AuditsAnalysis,
            SortIndex = 350,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.BrokenLinkChecker))
        });

        // ── Configuration & Admin ──────────────────────────────────
        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ContentTypeRecommendations",
            Title = "/editorpowertools/tools/contenttyperecommendations/title",
            Description = "/editorpowertools/tools/contenttyperecommendations/description",
            Url = Url("EditorPowertools/ContentTypeRecommendations"),
            IconSvgPath = SvgIcons.ContentTypeRecommendations,
            Group = PowertoolGroups.ConfigurationAdmin,
            SortIndex = 400,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ContentTypeRecommendations))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "AudienceManager",
            Title = "/editorpowertools/tools/audiencemanager/title",
            Description = "/editorpowertools/tools/audiencemanager/description",
            Url = Url("EditorPowertools/AudienceManager"),
            IconSvgPath = SvgIcons.AudienceManager,
            Group = PowertoolGroups.ConfigurationAdmin,
            SortIndex = 410,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.AudienceManager))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "CmsDoctor",
            Title = "/editorpowertools/tools/cmsdoctor/title",
            Description = "/editorpowertools/tools/cmsdoctor/description",
            Url = Url("EditorPowertools/CmsDoctor"),
            IconSvgPath = SvgIcons.CmsDoctor,
            Group = PowertoolGroups.ConfigurationAdmin,
            SortIndex = 420,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.CmsDoctor))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ActiveEditors",
            Title = "/editorpowertools/tools/activeeditors/title",
            Description = "/editorpowertools/tools/activeeditors/description",
            Url = Url("EditorPowertools/ActiveEditors"),
            IconSvgPath = SvgIcons.ActiveEditors,
            Group = PowertoolGroups.ConfigurationAdmin,
            SortIndex = 430,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ActiveEditors))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "ScheduledJobsGantt",
            Title = "/editorpowertools/tools/scheduledjobsgantt/title",
            Description = "/editorpowertools/tools/scheduledjobsgantt/description",
            Url = Url("EditorPowertools/ScheduledJobsGantt"),
            IconSvgPath = SvgIcons.ScheduledJobsGantt,
            Group = PowertoolGroups.ConfigurationAdmin,
            SortIndex = 440,
            AvailabilityCheck = ctx => Available(ctx, nameof(FeatureToggles.ScheduledJobsGantt))
        });

        return services;
    }
}

/// <summary>
/// Inline SVG path data for built-in tool icons. Each constant is the inner
/// markup of a 24x24 stroked icon (no surrounding &lt;svg&gt; tag).
/// </summary>
internal static class SvgIcons
{
    public const string ContentStatistics =
        @"<path d=""M18 20V10""/><path d=""M12 20V4""/><path d=""M6 20v-6""/>";

    public const string ActivityTimeline =
        @"<rect x=""3"" y=""4"" width=""18"" height=""18"" rx=""2"" ry=""2""/><line x1=""16"" y1=""2"" x2=""16"" y2=""6""/><line x1=""8"" y1=""2"" x2=""8"" y2=""6""/><line x1=""3"" y1=""10"" x2=""21"" y2=""10""/>";

    public const string BulkPropertyEditor =
        @"<path d=""M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7""/><path d=""M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z""/>";

    public const string ContentImporter =
        @"<path d=""M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4""/><polyline points=""17 8 12 3 7 8""/><line x1=""12"" y1=""3"" x2=""12"" y2=""15""/>";

    public const string ContentAudit =
        @"<path d=""M9 5H7a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-2""/><rect x=""9"" y=""3"" width=""6"" height=""4"" rx=""1""/><path d=""M9 12h6""/><path d=""M9 16h6""/>";

    public const string ContentTypeAudit =
        @"<path d=""M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7""/><path d=""M14 3v4a1 1 0 0 0 1 1h4""/>";

    public const string PersonalizationAudit =
        @"<path d=""M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2""/><circle cx=""9"" cy=""7"" r=""4""/><path d=""M23 21v-2a4 4 0 0 0-3-3.87""/><path d=""M16 3.13a4 4 0 0 1 0 7.75""/>";

    public const string LanguageAudit =
        @"<circle cx=""12"" cy=""12"" r=""10""/><line x1=""2"" y1=""12"" x2=""22"" y2=""12""/><path d=""M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z""/>";

    public const string SecurityAudit =
        @"<path d=""M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z""/>";

    public const string LinkChecker =
        @"<path d=""M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71""/><path d=""M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71""/>";

    public const string ContentTypeRecommendations =
        @"<path d=""M12 3h7a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-7m0-18H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h7m0-18v18""/>";

    public const string AudienceManager =
        @"<path d=""M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2""/><circle cx=""9"" cy=""7"" r=""4""/><circle cx=""19"" cy=""11"" r=""2""/><path d=""M19 8v6m-3-3h6""/>";

    public const string CmsDoctor =
        @"<path d=""M22 12h-4l-3 9L9 3l-3 9H2""/>";

    public const string ActiveEditors =
        @"<path d=""M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2""/><circle cx=""9"" cy=""7"" r=""4""/><circle cx=""19"" cy=""11"" r=""2""/>";

    public const string ScheduledJobsGantt =
        @"<path d=""M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z""/>";
}
