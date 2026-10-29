using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Forms.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.SampleSite.Extensions;
using EPiServer.Cms.Shell;
using EPiServer.Cms.UI.AspNetIdentity;
using EPiServer.DependencyInjection;
using EPiServer.Scheduler;
using EPiServer.ServiceLocation;
using EPiServer.Web.Routing;

namespace UmageAI.Optimizely.EditorPowerTools.SampleSite;

public class Startup
{
    private readonly IWebHostEnvironment _webHostingEnvironment;
    private readonly IConfiguration _configuration;

    public Startup(IWebHostEnvironment webHostingEnvironment, IConfiguration configuration)
    {
        _webHostingEnvironment = webHostingEnvironment;
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        if (_webHostingEnvironment.IsDevelopment())
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", Path.Combine(_webHostingEnvironment.ContentRootPath, "App_Data"));

            services.Configure<SchedulerOptions>(options => options.Enabled = false);
        }

        services
            .AddCmsAspNetIdentity<ApplicationUser>()
            .AddCms()
            .AddAlloy()
            .AddAdminUserRegistration()
            .AddEmbeddedLocalization<Startup>();

        
        // Editor Powertools
        services.AddEditorPowertools(options =>
        {
            // Configure options here or in appsettings.json under "CodeArt:EditorPowertools"
        });

        // Editor Powertools — Forms add-on. AddEditorPowertoolsForms() also
        // wires up EPiServer.Forms itself: a no-op on CMS 12 (5.x self-inits)
        // and AddFormsUI() on CMS 13 (6.x dropped the InitializableModule).
        services.AddEditorPowertoolsForms();

        // Optimizely Graph content sync + Graph add-on.
        services.AddContentDeliveryApi();
        services.AddContentGraph(options =>
        {
            options.IncludeInheritanceInContentType = true;
        });
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
