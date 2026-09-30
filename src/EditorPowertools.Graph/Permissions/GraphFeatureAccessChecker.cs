using EPiServer.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

/// <summary>
/// Graph-specific feature/permission gate. Mirrors the base
/// <see cref="FeatureAccessChecker"/> but reads its toggles from
/// <see cref="GraphFeatureToggles"/> while delegating per-user permission
/// checks to the base checker.
/// </summary>
public class GraphFeatureAccessChecker
{
    private readonly IOptions<EditorPowertoolsGraphOptions> _options;
    private readonly FeatureAccessChecker _baseChecker;

    public GraphFeatureAccessChecker(
        IOptions<EditorPowertoolsGraphOptions> options,
        FeatureAccessChecker baseChecker)
    {
        _options = options;
        _baseChecker = baseChecker;
    }

    public bool IsFeatureEnabled(string featureName)
    {
        var toggles = _options.Value.Features;
        var property = typeof(GraphFeatureToggles).GetProperty(featureName);
        if (property == null)
            return false;

        return (bool)(property.GetValue(toggles) ?? false);
    }

    public bool HasAccess(HttpContext context, string featureName, PermissionType permissionType)
    {
        return IsFeatureEnabled(featureName) && _baseChecker.HasPermission(context, permissionType);
    }

    /// <summary>
    /// For surfaces invoked by the CMS UI rather than our controllers (search providers, slices):
    /// applies the same "codeart:editorpowertools" policy the controllers carry, then the toggle and
    /// permission. Keeps these surfaces on exactly the same audience as every other tool.
    /// </summary>
    public bool HasUiAccess(HttpContext? context, string featureName, PermissionType permissionType)
    {
        if (context?.User?.Identity?.IsAuthenticated != true) return false;
        var auth = context.RequestServices.GetService<IAuthorizationService>();
        if (auth is null) return false;
        var policy = auth.AuthorizeAsync(context.User, "codeart:editorpowertools").GetAwaiter().GetResult();
        return policy.Succeeded && HasAccess(context, featureName, permissionType);
    }
}
