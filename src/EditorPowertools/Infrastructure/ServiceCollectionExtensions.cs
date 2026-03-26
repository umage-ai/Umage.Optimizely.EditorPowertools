using EditorPowertools.Configuration;
using EditorPowertools.Permissions;
using EditorPowertools.Services;
using EditorPowertools.Tools.ContentTypeAudit;
using EPiServer.Shell.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        return services.AddEditorPowertools(configureOptions, policy =>
            policy.RequireRole("WebAdmins", "CmsAdmins", "Administrators"));
    }

    /// <summary>
    /// Adds Editor Powertools with custom options and authorization policy.
    /// </summary>
    public static IServiceCollection AddEditorPowertools(
        this IServiceCollection services,
        Action<EditorPowertoolsOptions> configureOptions,
        Action<AuthorizationPolicyBuilder> configurePolicy)
    {
        // Bind options from appsettings section, then apply code overrides
        services.AddOptions<EditorPowertoolsOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("CodeArt:EditorPowertools").Bind(options);
            })
            .Configure(configureOptions);

        // Authorization policy
        services.AddAuthorization(options =>
        {
            options.AddPolicy("codeart:editorpowertools", configurePolicy);
        });

        // Core services
        services.AddSingleton<FeatureAccessChecker>();
        services.AddSingleton<ContentTypeStatisticsRepository>();

        // Tool services
        services.AddTransient<ContentTypeAuditService>();
        services.AddTransient<AggregationJobStatusService>();

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
