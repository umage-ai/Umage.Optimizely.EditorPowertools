using UmageAI.Optimizely.EditorPowerTools.Configuration;
using EPiServer.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UmageAI.Optimizely.EditorPowerTools.Permissions;

/// <summary>
/// Checks whether a user has access to a specific feature based on:
/// 1. Feature toggle (is the feature enabled at all?)
/// 2. EPiServer permission (optional, per-user check if CheckPermissionForEachFeature is true)
/// </summary>
public class FeatureAccessChecker
{
    private readonly IOptions<EditorPowertoolsOptions> _options;
    private readonly PermissionService _permissionService;

    public FeatureAccessChecker(
        IOptions<EditorPowertoolsOptions> options,
        PermissionService permissionService)
    {
        _options = options;
        _permissionService = permissionService;
    }

    /// <summary>
    /// Checks if the given feature is enabled via feature toggles.
    /// </summary>
    public bool IsFeatureEnabled(string featureName)
    {
        var toggles = _options.Value.Features;
        var property = typeof(FeatureToggles).GetProperty(featureName);
        if (property == null)
            return false;

        return (bool)(property.GetValue(toggles) ?? false);
    }

    /// <summary>
    /// Checks if the user has permission for a specific feature.
    /// Returns true if CheckPermissionForEachFeature is false (skip per-user checks).
    /// </summary>
    public bool HasPermission(HttpContext context, PermissionType permissionType)
    {
        if (!_options.Value.CheckPermissionForEachFeature)
            return true;

        return _permissionService.IsPermitted(context.User, permissionType);
    }

    /// <summary>
    /// Full access check: feature enabled + user has permission.
    /// </summary>
    public bool HasAccess(HttpContext context, string featureName, PermissionType permissionType)
    {
        return IsFeatureEnabled(featureName) && HasPermission(context, permissionType);
    }
}
