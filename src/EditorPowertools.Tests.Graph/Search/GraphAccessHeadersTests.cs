using System.Security.Claims;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace EditorPowertools.Tests.Graph.Search;

public class GraphAccessHeadersTests
{
    [Fact]
    public void Anonymous_user_sends_empty_identity_so_graph_returns_nothing()
    {
        var headers = GraphAccessHeaders.Build(null);
        headers["cg-username"].Should().BeEmpty();
        headers["cg-roles"].Should().BeEmpty();
    }

    [Fact]
    public void Sends_username_and_distinct_roles_from_role_and_declared_role_claim_types()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "anna"),
            new Claim("role", "WebEditors"),
            new Claim(ClaimTypes.Role, "WebAdmins"),
            new Claim(ClaimTypes.Role, "WebEditors"),
            new Claim(ClaimTypes.Role, " "),
        }, "test", ClaimTypes.Name, ClaimTypes.Role);

        var headers = GraphAccessHeaders.Build(new ClaimsPrincipal(identity));

        headers["cg-username"].Should().Be("anna");
        headers["cg-roles"].Split(',').Should().BeEquivalentTo("WebEditors", "WebAdmins");
    }
}
