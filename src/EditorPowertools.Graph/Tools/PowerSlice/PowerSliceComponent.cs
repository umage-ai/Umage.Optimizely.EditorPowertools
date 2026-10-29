using EPiServer.Framework.Localization;
using EPiServer.ServiceLocation;
using EPiServer.Shell;
using EPiServer.Shell.ContentQuery;
using EPiServer.Shell.ViewComposition;
using Microsoft.AspNetCore.Http;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

/// <summary>
/// Puts the Power Slice widget in the assets pane. The widget is built from the registered
/// <see cref="PowerSliceBase"/> queries, so adding a slice server-side is all it takes for one to appear.
/// </summary>
[Component]
public class PowerSliceComponent : ComponentDefinitionBase
{
    // Component definitions are instantiated by the shell, so the parameterless constructor resolves
    // its dependencies through the service locator.
    public PowerSliceComponent()
        : this(
            ServiceLocator.Current.GetAllInstances<IContentQuery>(),
            ServiceLocator.Current.GetInstance<LocalizationService>(),
            ServiceLocator.Current.GetInstance<GraphFeatureAccessChecker>(),
            ServiceLocator.Current.GetInstance<IHttpContextAccessor>())
    {
    }

    public PowerSliceComponent(
        IEnumerable<IContentQuery> queries,
        LocalizationService localization,
        GraphFeatureAccessChecker access,
        IHttpContextAccessor http)
        : base("editorpowertools-graph/PowerSliceWidget")
    {
        LanguagePath = "/editorpowertools/components/powerslice";
        Categories = new[] { "cms" };
        PlugInAreas = new[] { PlugInArea.AssetsDefaultGroup };
        SortOrder = 1100;

        // Only known once a request exists. Without one (shell startup) the widget stays enabled and the
        // slices themselves return nothing for callers who lack access.
        var context = http.HttpContext;
        var enabled = context is null
            || access.HasUiAccess(context, nameof(GraphFeatureToggles.PowerSlice), EditorPowertoolsGraphPermissions.PowerSlice);

        var slices = (queries ?? Array.Empty<IContentQuery>())
            .OfType<PowerSliceBase>()
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.DisplayName)
            .Select(s => new
            {
                name = s.Name,
                displayName = string.IsNullOrWhiteSpace(s.DisplayName) ? s.Name : s.DisplayName,
                sortOptions = s.SortOptions
                    .Select(o => new { key = o.Key, label = localization.GetString(o.LabelPath) })
                    .ToList()
            })
            .ToList();

        // Cast to object: Setting(string, bool) means "personalizable", not "value".
        Settings.Add(new Setting("enabled", (object)enabled));
        Settings.Add(new Setting("slices", slices));
        Settings.Add(new Setting("strings", new
        {
            filterPlaceholder = localization.GetString("/editorpowertools/graph/powerslice/strings/filterplaceholder"),
            reload = localization.GetString("/editorpowertools/graph/powerslice/strings/reload"),
            notAvailable = localization.GetString("/editorpowertools/graph/powerslice/strings/notavailable"),
            empty = localization.GetString("/editorpowertools/graph/powerslice/strings/empty")
        }));
    }
}
