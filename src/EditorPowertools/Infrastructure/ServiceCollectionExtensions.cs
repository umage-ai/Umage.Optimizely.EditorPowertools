using EditorPowertools.Configuration;
using EditorPowertools.Permissions;
using EditorPowertools.Services;
using EditorPowertools.Tools.AudienceManager;
using EditorPowertools.Tools.BulkPropertyEditor;
using EditorPowertools.Tools.ActivityTimeline;
using EditorPowertools.Tools.ContentDetails;
using EditorPowertools.Tools.ContentTypeAudit;
using EditorPowertools.Tools.ContentImporter;
using EditorPowertools.Tools.ContentImporter.Parsers;
using EditorPowertools.Tools.LinkChecker;
using EditorPowertools.Tools.ScheduledJobsGantt;
using EditorPowertools.Tools.ContentTypeRecommendations;
using EditorPowertools.Services.Analyzers;
using EditorPowertools.Tools.PersonalizationAudit;
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

        // Link Checker
        services.AddSingleton<LinkCheckerRepository>();
        services.AddTransient<LinkCheckerService>();
        services.AddTransient<LinkCheckerJobStatusService>();
        services.AddHttpClient();

        // Unified content analysis (pluggable analyzers)
        services.AddTransient<IContentAnalyzer, ContentTypeStatisticsAnalyzer>();
        services.AddTransient<IContentAnalyzer, PersonalizationAnalyzer>();
        services.AddTransient<IContentAnalyzer, LinkCheckerAnalyzer>();

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
