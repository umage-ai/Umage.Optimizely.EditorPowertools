using System.Text.Json;
using EPiServer.Framework.Localization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;
using EPiServer.Security;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Tools.PowerSearch;

public class PowerSearchApiControllerTests
{
    private readonly Mock<IPowerSearchService> _service = new();
    private readonly Mock<IGraphSearchService> _graph = new();
    private readonly Mock<GraphFeatureAccessChecker> _access =
        new(Microsoft.Extensions.Options.Options.Create(new EditorPowertoolsGraphOptions()), null!);
    private PowerSearchQuery? _captured;

    private static readonly PowerSearchResponseDto Ok = new(
        Array.Empty<PowerSearchHitDto>(), 0, null, PowerSearchFacetsDto.Empty, Array.Empty<PowerSearchTabCountDto>(), "ok");

    public PowerSearchApiControllerTests()
    {
        _access.Setup(a => a.HasAccess(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<PermissionType>())).Returns(true);
        _graph.Setup(g => g.ValidateContentTypes(It.IsAny<IEnumerable<string>?>()))
            .Returns((IEnumerable<string>? n) => (n ?? Array.Empty<string>()).Where(x => x != "Bogus").ToList());
        _service.Setup(s => s.SearchAsync(It.IsAny<PowerSearchQuery>(), It.IsAny<CancellationToken>()))
            .Callback((PowerSearchQuery q, CancellationToken _) => _captured = q)
            .ReturnsAsync(Ok);
    }

    private PowerSearchApiController Create() => new(
        _service.Object, _graph.Object, _access.Object, new MemoryLocalizationService(),
        Mock.Of<ILogger<PowerSearchApiController>>())
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task Forbidden_when_feature_or_permission_denied()
    {
        _access.Setup(a => a.HasAccess(It.IsAny<HttpContext>(), "PowerSearch", It.IsAny<PermissionType>())).Returns(false);
        var result = await Create().Search("abc");
        result.Should().BeOfType<ForbidResult>();
        _service.Verify(s => s.SearchAsync(It.IsAny<PowerSearchQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_tab_is_a_bad_request()
    {
        var result = await Create().Search("abc", tab: (PowerSearchTab)99);
        result.Should().BeOfType<BadRequestResult>();
        _service.Verify(s => s.SearchAsync(It.IsAny<PowerSearchQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invalid_section_guids_are_dropped()
    {
        var good = Guid.NewGuid();
        await Create().Search("abc", section: new[] { "not-a-guid", good.ToString(), "", Guid.Empty.ToString() });
        _captured!.Sections.Should().Equal(good);
    }

    [Fact]
    public async Task Unknown_type_names_are_dropped()
    {
        await Create().Search("abc", type: new[] { "ArticlePage", "Bogus" });
        _captured!.Types.Should().Equal("ArticlePage");
    }

    [Fact]
    public async Task Invalid_languages_are_dropped_and_count_is_capped()
    {
        var langs = new[] { "fr-CH", "not a culture !!!" }
            .Concat(new[] { "en", "da", "sv", "no", "de", "fi", "fr", "es", "nl", "ja", "it", "pt", "ru", "pl", "cs", "hu", "tr", "el", "he", "ar", "ko" })
            .ToArray();
        await Create().Search("abc", lang: langs);
        _captured!.Languages.Should().NotContain("not a culture !!!");
        _captured.Languages.Should().HaveCount(20);
        _captured.Languages[0].Should().Be("fr-CH");
    }

    [Fact]
    public async Task Exception_yields_500_without_exception_text()
    {
        _service.Setup(s => s.SearchAsync(It.IsAny<PowerSearchQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret-marker"));

        var result = await Create().Search("abc");

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(500);
        JsonSerializer.Serialize(obj.Value).Should().NotContain("secret-marker");
    }

    [Fact]
    public async Task Success_returns_service_response()
    {
        var result = await Create().Search("abc", PowerSearchTab.Media, 5, 10);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(Ok);
        _captured!.Tab.Should().Be(PowerSearchTab.Media);
        (_captured.Take, _captured.Skip).Should().Be((5, 10));
    }
}
