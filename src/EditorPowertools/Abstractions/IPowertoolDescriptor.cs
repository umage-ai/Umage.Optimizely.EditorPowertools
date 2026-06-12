using Microsoft.AspNetCore.Http;

namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

/// <summary>
/// Describes a tool tile rendered on the Overview dashboard. Built-in tools
/// register an <see cref="IPowertoolDescriptor"/> via DI; third-party add-ons
/// (e.g. the Forms add-on) register their own without the base library having
/// to know about them.
/// </summary>
/// <remarks>
/// The Overview view enumerates <c>IEnumerable&lt;IPowertoolDescriptor&gt;</c>
/// from the request <see cref="IServiceProvider"/>, filters by
/// <see cref="IsAvailable"/>, and renders one card per descriptor sorted by
/// <see cref="Group"/> then <see cref="SortIndex"/>.
/// </remarks>
public interface IPowertoolDescriptor
{
    /// <summary>Stable identifier — only used for diagnostics / dedup.</summary>
    string Id { get; }

    /// <summary>Localized display title shown on the card.</summary>
    string Title { get; }

    /// <summary>Localized one-line description shown under the title.</summary>
    string Description { get; }

    /// <summary>
    /// Absolute URL the card links to. Use
    /// <c>EPiServer.Shell.Paths.ToResource(typeof(MyMenuProvider), "Controller/Action")</c>
    /// to build this so the right module mount point is used.
    /// </summary>
    string Url { get; }

    /// <summary>
    /// Inline SVG icon markup (just the contents — no outer &lt;svg&gt; element).
    /// The view wraps this in a 24x24 stroked SVG.
    /// </summary>
    string IconSvgPath { get; }

    /// <summary>Display group used for ordering on the Overview.</summary>
    string Group { get; }

    /// <summary>Order within the group, ascending.</summary>
    int SortIndex { get; }

    /// <summary>
    /// Returns true if this tool should appear for the current user/feature toggles.
    /// Implementations typically delegate to <c>FeatureAccessChecker</c> /
    /// <c>FormsFeatureAccessChecker</c>.
    /// </summary>
    bool IsAvailable(HttpContext context);
}
