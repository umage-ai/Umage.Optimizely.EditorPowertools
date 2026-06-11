using EPiServer.Shell;
using EPiServer.Shell.Modules;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Forms.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Forms.Menu;
using UmageAI.Optimizely.EditorPowerTools.Forms.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.Diagnostics.Checks;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Editor Powertools Forms add-on services with default options.
    /// Call this AFTER <c>AddEditorPowertools()</c>.
    /// </summary>
    public static IServiceCollection AddEditorPowertoolsForms(this IServiceCollection services)
        => services.AddEditorPowertoolsForms(_ => { });

    /// <summary>
    /// Registers the Editor Powertools Forms add-on services with custom options.
    /// </summary>
    public static IServiceCollection AddEditorPowertoolsForms(
        this IServiceCollection services,
        Action<EditorPowertoolsFormsOptions> configureOptions)
    {
        services.AddOptions<EditorPowertoolsFormsOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("CodeArt:EditorPowertools:Forms").Bind(options);
            })
            .Configure(configureOptions);

        services.AddSingleton<FormsFeatureAccessChecker>();
        services.AddTransient<IFormsAggregationService, FormsAggregationService>();
        // Broadcaster is a singleton because it hooks the static FormsEvents
        // exactly once and fans out to many SSE clients. AddHostedService keeps
        // it alive for the app lifetime and disposes cleanly on shutdown so the
        // event handlers get unhooked.
        services.AddSingleton<SubmissionsBroadcaster>();
        services.AddHostedService(sp => new SubmissionsBroadcasterHost(sp.GetRequiredService<SubmissionsBroadcaster>()));

        // CMS Doctor checks (third-party plug-in into the base library's IDoctorCheck
        // collection — the Doctor service in the base discovers them via DI).
        services.AddTransient<IDoctorCheck, UnusedFormsCheck>();
        services.AddTransient<IDoctorCheck, NoNotificationHandlerCheck>();
        services.AddTransient<IDoctorCheck, PiiIndefiniteRetentionCheck>();
        services.AddTransient<IDoctorCheck, DuplicateFieldsCheck>();

        // Plug into the base Overview without the base library having to know
        // about Forms. Each descriptor is filtered by its feature toggle.
        string FormsUrl(string path) => Paths.ToResource(typeof(EditorPowertoolsFormsMenuProvider), path);
        bool FormsAvailable(HttpContext ctx, string feature) =>
            ctx.RequestServices.GetService<FormsFeatureAccessChecker>()?.IsFeatureEnabled(feature) ?? true;

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "FormsOverview",
            Title = "/editorpowertools/tools/formsoverview/title",
            Description = "/editorpowertools/tools/formsoverview/description",
            Url = FormsUrl("EditorPowertoolsForms/FormsOverview"),
            IconSvgPath = FormsSvgIcons.FormsOverview,
            Group = PowertoolGroups.Forms,
            SortIndex = 360,
            AvailabilityCheck = ctx => FormsAvailable(ctx, nameof(FormsFeatureToggles.FormsOverview))
        });

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "SubmissionsTimeline",
            Title = "/editorpowertools/tools/submissionstimeline/title",
            Description = "/editorpowertools/tools/submissionstimeline/description",
            Url = FormsUrl("EditorPowertoolsForms/SubmissionsTimeline"),
            IconSvgPath = FormsSvgIcons.SubmissionsTimeline,
            Group = PowertoolGroups.Forms,
            SortIndex = 365,
            AvailabilityCheck = ctx => FormsAvailable(ctx, nameof(FormsFeatureToggles.SubmissionsTimeline))
        });

        // Register the Forms add-on as its own protected module so it can ship
        // its own ClientResources and Razor controllers without conflicting with
        // the base EditorPowertools module.
        services.Configure<ProtectedModuleOptions>(options =>
        {
            if (!options.Items.Any(m => m.Name == "EditorPowertoolsForms"))
            {
                options.Items.Add(new ModuleDetails { Name = "EditorPowertoolsForms" });
            }
        });

        return services;
    }
}
