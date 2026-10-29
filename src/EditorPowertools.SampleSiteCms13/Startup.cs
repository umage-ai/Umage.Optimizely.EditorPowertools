using EditorPowertools.SampleSiteCms13.Extensions;
using EPiServer.Cms.UI.AspNetIdentity;
using EPiServer.Data;
using EPiServer.DependencyInjection;
using Optimizely.Graph.DependencyInjection;
using EPiServer.Scheduler;
using EPiServer.Web.Routing;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Forms.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;

namespace EditorPowertools.SampleSiteCms13;

public class Startup(IWebHostEnvironment webHostingEnvironment, IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        if (webHostingEnvironment.IsDevelopment())
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", Path.Combine(webHostingEnvironment.ContentRootPath, "App_Data"));

            services.Configure<SchedulerOptions>(options => options.Enabled = false);
        }

        services.Configure<DataAccessOptions>(o => o.UpdateDatabaseCompatibilityLevel = true);

        services
            .AddCmsAspNetIdentity<ApplicationUser>()
            .AddCms()
            .AddAlloy()
            .AddAdminUserRegistration()
            .AddEmbeddedLocalization<Startup>();

        // EPiServer.Forms 6.x (CMS 13) dropped the InitializableModule self-registration
        // that 5.x shipped with, so the host must call services.AddForms() explicitly.
        services.AddForms();

        services.AddEditorPowertools();
        services.AddEditorPowertoolsForms();

        // Optimizely Graph content sync + Graph add-on. On CMS 13 AddContentGraph makes the
        // schema-sync init module call the gateway at startup and throws on an empty
        // GatewayAddress, so it is only registered when credentials are configured.
        if (!string.IsNullOrEmpty(configuration["Optimizely:ContentGraph:AppKey"]))
        {
            services.AddContentGraph(options =>
            {
                // CMS 13 Graph 13.2.0 exposes no IncludeInheritanceInContentType option (see plan Task 16).
            });
        }
        services.AddEditorPowertoolsGraph();

        // Required by Wangkanai.Detection
        services.AddDetection();

        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromSeconds(10);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Required by Wangkanai.Detection
        app.UseDetection();
        app.UseSession();

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEditorPowertools();
        app.UseEditorPowertoolsForms();
        app.UseEditorPowertoolsGraph();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapContent();
            endpoints.MapEditorPowertools();
            endpoints.MapEditorPowertoolsForms();
            endpoints.MapEditorPowertoolsGraph();
        });
    }
}
