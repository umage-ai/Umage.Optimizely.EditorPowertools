using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Permissions;

public class GraphFeatureAccessCheckerTests
{
    private static GraphFeatureAccessChecker Create(Action<GraphFeatureToggles>? toggles = null)
    {
        var graph = new EditorPowertoolsGraphOptions();
        toggles?.Invoke(graph.Features);
        var core = new FeatureAccessChecker(
            Options.Create(new EditorPowertoolsOptions { CheckPermissionForEachFeature = false }),
            permissionService: null!);
        return new GraphFeatureAccessChecker(Options.Create(graph), core);
    }

    [Theory]
    [InlineData(nameof(GraphFeatureToggles.PowerSearch))]
    [InlineData(nameof(GraphFeatureToggles.PowerSlice))]
    [InlineData(nameof(GraphFeatureToggles.SearchProviders))]
    public void Features_default_to_enabled(string feature) =>
        Create().IsFeatureEnabled(feature).Should().BeTrue();

    [Fact]
    public void Disabled_toggle_denies_access()
    {
        var checker = Create(t => t.PowerSearch = false);
        checker.HasAccess(new DefaultHttpContext(), nameof(GraphFeatureToggles.PowerSearch),
            EditorPowertoolsGraphPermissions.PowerSearch).Should().BeFalse();
    }

    [Fact]
    public void Unknown_feature_fails_closed() => Create().IsFeatureEnabled("Nope").Should().BeFalse();

    [Fact]
    public void Ui_access_denies_anonymous() =>
        Create().HasUiAccess(new DefaultHttpContext(), nameof(GraphFeatureToggles.SearchProviders),
            EditorPowertoolsGraphPermissions.SearchProviders).Should().BeFalse();

    [Fact]
    public void Ui_access_requires_policy()
    {
        var auth = new Mock<IAuthorizationService>();
        auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "codeart:editorpowertools"))
            .ReturnsAsync(AuthorizationResult.Failed());
        var ctx = AuthenticatedContext(auth.Object);
        Create().HasUiAccess(ctx, nameof(GraphFeatureToggles.SearchProviders),
            EditorPowertoolsGraphPermissions.SearchProviders).Should().BeFalse();

        auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "codeart:editorpowertools"))
            .ReturnsAsync(AuthorizationResult.Success());
        Create().HasUiAccess(ctx, nameof(GraphFeatureToggles.SearchProviders),
            EditorPowertoolsGraphPermissions.SearchProviders).Should().BeTrue();
    }

    private static HttpContext AuthenticatedContext(IAuthorizationService auth)
    {
        var services = new ServiceCollection().AddSingleton(auth).BuildServiceProvider();
        return new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ed") }, "test"))
        };
    }
}
