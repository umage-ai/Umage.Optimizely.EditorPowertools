using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;
#if OPTIMIZELY_CMS13
using Optimizely.Graph.Core.Client;
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms13;
#else
using Optimizely.ContentGraph.Core;
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms12;
#endif

namespace EditorPowertools.Tests.Graph.Search;

/// <summary>
/// Pins the real executor for the current TFM: the per-request identity headers are what make Graph
/// apply the editor's read permissions, so dropping or hard-coding them must fail a test.
/// </summary>
public class GraphQueryExecutorTests
{
    private static readonly GraphRequest Request =
        new("query { Content { items { Name } } }", new Dictionary<string, object?> { ["phrase"] = "x" });

#if OPTIMIZELY_CMS13
    private static Mock<IGraphClient> NewClient() => new();
    private static IGraphQueryExecutor NewExecutor(IServiceProvider sp, IHttpContextAccessor http) => new Cms13GraphQueryExecutor(sp, http);

    private static void Setup(Mock<IGraphClient> client, Func<IDictionary<string, string>, Task<string>> reply) =>
        client.Setup(c => c.QueryAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns((string _, object _, IDictionary<string, string> headers, CancellationToken _) => reply(headers));
#else
    private static Mock<IClient> NewClient() => new();
    private static IGraphQueryExecutor NewExecutor(IServiceProvider sp, IHttpContextAccessor http) => new Cms12GraphQueryExecutor(sp, http);

    private static void Setup(Mock<IClient> client, Func<IDictionary<string, string>, Task<string>> reply) =>
        client.Setup(c => c.QueryAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, object _, Dictionary<string, string>? headers, Dictionary<string, string>? _, CancellationToken _) => reply(headers!));
#endif

    private static IServiceProvider Services(object? client)
    {
        var sc = new ServiceCollection();
        if (client is not null)
        {
#if OPTIMIZELY_CMS13
            sc.AddSingleton((IGraphClient)client);
#else
            sc.AddSingleton((IClient)client);
#endif
        }
        return sc.BuildServiceProvider();
    }

    private static IHttpContextAccessor Http(ClaimsPrincipal? user) =>
        new HttpContextAccessor { HttpContext = user is null ? null : new DefaultHttpContext { User = user } };

    private static ClaimsPrincipal Editor() => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Name, "anna"),
        new Claim(ClaimTypes.Role, "WebEditors")
    }, "test", ClaimTypes.Name, ClaimTypes.Role));

    [Fact]
    public void Client_availability_follows_registration()
    {
        NewExecutor(Services(null), Http(null)).IsClientAvailable.Should().BeFalse();
        NewExecutor(Services(NewClient().Object), Http(null)).IsClientAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Sends_the_current_editors_identity_headers()
    {
        var client = NewClient();
        IDictionary<string, string>? seen = null;
        Setup(client, h => { seen = h; return Task.FromResult("""{"data":{"Content":{"items":[]}}}"""); });

        var data = await NewExecutor(Services(client.Object), Http(Editor())).ExecuteAsync(Request, CancellationToken.None);

        seen.Should().NotBeNull();
        seen!["cg-username"].Should().Be("anna");
        seen["cg-roles"].Should().Be("WebEditors");
        data.TryGetProperty("Content", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Without_http_context_sends_empty_identity_not_an_elevated_one()
    {
        var client = NewClient();
        IDictionary<string, string>? seen = null;
        Setup(client, h => { seen = h; return Task.FromResult("""{"data":{}}"""); });

        await NewExecutor(Services(client.Object), Http(null)).ExecuteAsync(Request, CancellationToken.None);

        seen!["cg-username"].Should().BeEmpty();
        seen["cg-roles"].Should().BeEmpty();
    }

    [Fact]
    public async Task Client_failure_becomes_GraphQueryException_without_leaking_detail()
    {
        var client = NewClient();
        Setup(client, _ => throw new InvalidOperationException("secret gateway detail"));

        var act = () => NewExecutor(Services(client.Object), Http(Editor())).ExecuteAsync(Request, CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<GraphQueryException>()).Which;
        ex.Message.Should().NotContain("secret");
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancellation_is_not_wrapped()
    {
        var client = NewClient();
        Setup(client, _ => throw new OperationCanceledException());

        var act = () => NewExecutor(Services(client.Object), Http(Editor())).ExecuteAsync(Request, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Missing_client_throws_GraphQueryException()
    {
        var act = () => NewExecutor(Services(null), Http(Editor())).ExecuteAsync(Request, CancellationToken.None);
        await act.Should().ThrowAsync<GraphQueryException>();
    }
}
