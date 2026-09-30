using System.Security.Claims;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>
/// Builds the per-request identity headers so Graph applies the current editor's read permissions.
/// An anonymous/absent user sends empty values, which makes Graph return only public content.
/// </summary>
public static class GraphAccessHeaders
{
    public const string UsernameHeader = "cg-username";
    public const string RolesHeader = "cg-roles";

    public static Dictionary<string, string> Build(ClaimsPrincipal? user)
    {
        return new Dictionary<string, string>
        {
            [UsernameHeader] = user?.Identity?.Name ?? string.Empty,
            [RolesHeader] = string.Join(",", ResolveRoles(user)),
        };
    }

    private static IEnumerable<string> ResolveRoles(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return Enumerable.Empty<string>();
        }

        var roleTypes = new HashSet<string>(StringComparer.Ordinal) { "role" };
        foreach (var identity in user.Identities)
        {
            if (!string.IsNullOrEmpty(identity.RoleClaimType))
            {
                roleTypes.Add(identity.RoleClaimType);
            }
        }

        return user.Claims
            .Where(c => roleTypes.Contains(c.Type) && !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => c.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}
