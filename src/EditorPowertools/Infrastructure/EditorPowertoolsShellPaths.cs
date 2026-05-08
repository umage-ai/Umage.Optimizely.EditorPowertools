using EPiServer.Shell;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

/// <summary>
/// Single source of truth for resolving paths to Optimizely shell modules
/// that the addon links to (CMS, Admin, Visitor Groups). Wraps
/// <see cref="Paths.ToResource(string, string)"/> so there is one place to fix if a
/// host environment registers these modules under a non-default name (e.g. Opti-ID
/// installs that mount the shell at <c>/ui/</c> instead of <c>/episerver/</c>).
///
/// Always prefer <see cref="Paths.ToResource(System.Type, string)"/> with a typeof()
/// from inside the addon's own assembly when linking to addon resources — those resolve
/// regardless of how the host is configured. This helper is only for cross-module links.
/// </summary>
internal static class EditorPowertoolsShellPaths
{
    /// <summary>Root path of the CMS edit UI module (e.g. <c>/episerver/CMS/</c>).</summary>
    public static string CmsRoot() => Paths.ToResource("CMS", "");

    /// <summary>Default page of the legacy admin SPA (e.g. <c>/episerver/EPiServer.Cms.UI.Admin/default</c>).</summary>
    public static string AdminDefault() => Paths.ToResource("EPiServer.Cms.UI.Admin", "default");

    /// <summary>Manage-visitor-groups page in the CMS UI.</summary>
    public static string ManageVisitorGroups() => Paths.ToResource("EPiServer.Cms.UI.VisitorGroups", "ManageVisitorGroups");
}
