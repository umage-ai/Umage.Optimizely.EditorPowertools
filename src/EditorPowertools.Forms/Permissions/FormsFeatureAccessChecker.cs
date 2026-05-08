using UmageAI.Optimizely.EditorPowerTools.Forms.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using EPiServer.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Permissions;

/// <summary>
/// Forms-specific feature/permission gate. Mirrors the base
/// <see cref="FeatureAccessChecker"/> but reads its toggles from
/// <see cref="FormsFeatureToggles"/> while delegating per-user permission
/// checks to the base checker so consumers keep the same
/// <c>CheckPermissionForEachFeature</c> behavior.
/// </summary>
public class FormsFeatureAccessChecker
{
    private readonly IOptions<EditorPowertoolsFormsOptions> _options;
    private readonly FeatureAccessChecker _baseChecker;

    public FormsFeatureAccessChecker(
        IOptions<EditorPowertoolsFormsOptions> options,
        FeatureAccessChecker baseChecker)
    {
        _options = options;
        _baseChecker = baseChecker;
    }

    public bool IsFeatureEnabled(string featureName)
    {
        var toggles = _options.Value.Features;
        var property = typeof(FormsFeatureToggles).GetProperty(featureName);
        if (property == null)
            return false;

        return (bool)(property.GetValue(toggles) ?? false);
    }

    public bool HasAccess(HttpContext context, string featureName, PermissionType permissionType)
    {
        return IsFeatureEnabled(featureName) && _baseChecker.HasPermission(context, permissionType);
    }
}
