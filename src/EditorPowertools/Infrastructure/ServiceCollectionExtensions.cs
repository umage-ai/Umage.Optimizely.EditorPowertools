using EditorPowertools.Configuration;
using EditorPowertools.Localization;
using EditorPowertools.Permissions;
using EditorPowertools.Services;
using EditorPowertools.Tools.AudienceManager;
using EditorPowertools.Tools.BulkPropertyEditor;
using EditorPowertools.Tools.ActivityTimeline;
using EditorPowertools.Tools.ContentDetails;
using EditorPowertools.Tools.ContentTypeAudit;
using EditorPowertools.Tools.ContentImporter;
using EditorPowertools.Tools.ManageChildren;
using EditorPowertools.Tools.ContentImporter.Parsers;
using EditorPowertools.Tools.LinkChecker;
using EditorPowertools.Tools.ScheduledJobsGantt;
using EditorPowertools.Tools.ContentTypeRecommendations;
using EditorPowertools.Services.Analyzers;
using EditorPowertools.Tools.ActiveEditors;
using EditorPowertools.Tools.CmsDoctor;
using EditorPowertools.Tools.CmsDoctor.Checks;
using EditorPowertools.Tools.CmsDoctor.Models;
using EditorPowertools.Tools.ContentAudit;
using EditorPowertools.Tools.PersonalizationAudit;
using EditorPowertools.Tools.ContentStatistics;
using EditorPowertools.Tools.LanguageAudit;
using EditorPowertools.Tools.SecurityAudit;
using EPiServer.Shell.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EditorPowertools.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Editor Powertools with default options.
    /// </summary>
    public static IServiceCollection AddEditorPowertools(this IServiceCollection services)
    {
        return services.AddEditorPowertools(_ => { });
    }

    /// <summary>
    /// Adds Editor Powertools with custom options.
    /// </summary>
    public static IServiceCollection AddEditorPowertools(
        this IServiceCollection services,
        Action<EditorPowertoolsOptions> configureOptions)
    {
        // Bind options from appsettings section, then apply code overrides
        services.AddOptions<EditorPowertoolsOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("CodeArt:EditorPowertools").Bind(options);
            })
            .Configure(configureOptions);

        // Build the authorization policy from configured roles.
        // Uses PostConfigure to read the final merged options, then configures the policy.
        services.AddSingleton<IPostConfigureOptions<AuthorizationOptions>, ConfigureEditorPowertoolsPolicy>();

        // Core services
        services.AddSingleton<FeatureAccessChecker>();
        services.AddSingleton<ContentTypeStatisticsRepository>();
        services.AddSingleton<UserPreferencesService>();

        // Tool services
        services.AddTransient<ContentTypeAuditService>();
        services.AddTransient<AggregationJobStatusService>();

        // Personalization Audit
        services.AddSingleton<PersonalizationUsageRepository>();
        services.AddTransient<PersonalizationAuditService>();
        services.AddTransient<PersonalizationJobStatusService>();

        // Audience Manager
        services.AddTransient<AudienceManagerService>();

        // Content Type Recommendations
        services.AddSingleton<ContentTypeRecommendationRepository>();
        services.AddTransient<ContentTypeRecommendationService>();
        services.AddTransient<EPiServer.Cms.Shell.UI.Rest.IContentTypeAdvisor, ContentTypeRecommendationAdvisor>();

        // Bulk Property Editor
        services.AddTransient<BulkPropertyEditorService>();

        // Scheduled Jobs Gantt
        services.AddTransient<ScheduledJobsGanttService>();

        // Activity Timeline
        services.AddTransient<ActivityTimelineService>();

        // Content Details (assets panel widget)
        services.AddTransient<ContentDetailsService>();

        // Content Importer
        services.AddSingleton<ImportSessionStore>();
        services.AddTransient<ContentImporterService>();
        services.AddTransient<IFileParser, CsvFileParser>();
        services.AddTransient<IFileParser, JsonFileParser>();
        services.AddTransient<IFileParser, ExcelFileParser>();

        // Manage Children
        services.AddTransient<ManageChildrenService>();

        // CMS Doctor - built-in health checks (third-party can add more via IDoctorCheck)
        services.AddSingleton<DoctorCheckResultStore>();
        services.AddSingleton<CmsDoctorService>();
        services.AddTransient<IDoctorCheck, ContentTypeCheck>();
        services.AddTransient<IDoctorCheck, OrphanedPropertyCheck>();
        services.AddTransient<IDoctorCheck, ScheduledJobsCheck>();
        services.AddTransient<IDoctorCheck, DraftContentCheck>();
        services.AddTransient<IDoctorCheck, VersionInfoCheck>();
        services.AddTransient<IDoctorCheck, MemoryCheck>();
        services.AddTransient<IDoctorCheck, BrokenLinksCheck>();

        // Analyzer health checks — registered as both IDoctorCheck and IContentAnalyzer
        // so they collect data during the scheduled job and report on the dashboard
        services.AddSingleton<MissingAltTextCheck>();
        services.AddSingleton<IDoctorCheck>(sp => sp.GetRequiredService<MissingAltTextCheck>());
        services.AddSingleton<IContentAnalyzer>(sp => sp.GetRequiredService<MissingAltTextCheck>());
        services.AddSingleton<UnusedContentCheck>();
        services.AddSingleton<IDoctorCheck>(sp => sp.GetRequiredService<UnusedContentCheck>());
        services.AddSingleton<IContentAnalyzer>(sp => sp.GetRequiredService<UnusedContentCheck>());

        // Content Audit
        services.AddTransient<ContentAuditService>();
        services.AddTransient<IContentAuditDataProvider, GetDescendentsContentAuditProvider>();
        services.AddTransient<ContentAuditExportRenderer>();

        // Link Checker
        services.AddSingleton<LinkCheckerRepository>();
        services.AddTransient<LinkCheckerService>();
        services.AddTransient<LinkCheckerJobStatusService>();
        services.AddHttpClient();

        // Security Audit
        services.AddSingleton<SecurityAuditRepository>();
        services.AddTransient<SecurityAuditService>();
        services.AddTransient<IDoctorCheck, EveryonePublishRightsCheck>();
        services.AddTransient<IDoctorCheck, UnrestrictedContentCheck>();
        services.AddTransient<IDoctorCheck, InconsistentInheritanceCheck>();

        // Content Statistics
        services.AddTransient<ContentStatisticsService>();

        // Language Audit
        services.AddSingleton<LanguageAuditRepository>();
        services.AddTransient<LanguageAuditService>();

        // Unified content analysis (pluggable analyzers)
        services.AddTransient<IContentAnalyzer, ContentTypeStatisticsAnalyzer>();
        services.AddTransient<IContentAnalyzer, PersonalizationAnalyzer>();
        services.AddTransient<IContentAnalyzer, LinkCheckerAnalyzer>();
        services.AddTransient<IContentAnalyzer, SecurityAuditAnalyzer>();
        services.AddTransient<IContentAnalyzer, LanguageAuditAnalyzer>();

        // Active Editors (real-time presence + chat)
        services.AddSingleton<ActiveEditorsService>();
        services.AddSignalR();

        // UI strings provider for JS localization
        services.AddScoped<UiStringsProvider>();

        // Register as a protected module
        services.Configure<ProtectedModuleOptions>(options =>
        {
            options.Items.Add(new ModuleDetails
            {
                Name = "EditorPowertools"
            });
        });

        return services;
    }
}

/// <summary>
/// Configures the authorization policy using roles from EditorPowertoolsOptions.
/// This runs after all option configurations have been applied, so it sees the final merged roles.
/// </summary>
internal class ConfigureEditorPowertoolsPolicy : IPostConfigureOptions<AuthorizationOptions>
{
    private readonly IOptions<EditorPowertoolsOptions> _eptOptions;

    public ConfigureEditorPowertoolsPolicy(IOptions<EditorPowertoolsOptions> eptOptions)
    {
        _eptOptions = eptOptions;
    }

    public void PostConfigure(string? name, AuthorizationOptions options)
    {
        var roles = _eptOptions.Value.AuthorizedRoles;
        if (roles == null || roles.Length == 0)
            roles = ["WebAdmins", "Administrators"];

        options.AddPolicy("codeart:editorpowertools", policy => policy.RequireRole(roles));
    }
}
