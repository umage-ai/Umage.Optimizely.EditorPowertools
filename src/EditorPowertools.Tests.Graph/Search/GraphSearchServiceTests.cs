using System.Text.Json;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace EditorPowertools.Tests.Graph.Search;

public class GraphSearchServiceTests
{
    private readonly Mock<IGraphQueryDialect> _dialect = new();
    private readonly Mock<IGraphQueryExecutor> _executor = new();
    private readonly Mock<IGraphAvailability> _availability = new();
    private readonly Mock<IContentTypeRepository> _types = new();
    private GraphSearchRequest? _built;

    public GraphSearchServiceTests()
    {
        _availability.SetupGet(a => a.State).Returns(GraphAvailabilityState.Available);
        _availability.SetupGet(a => a.IsAvailable).Returns(true);
        _types.Setup(t => t.List()).Returns(new[] { new ContentType { Name = "ArticlePage" } });
        _dialect.Setup(d => d.BuildSearch(It.IsAny<GraphSearchRequest>()))
            .Callback<GraphSearchRequest>(r => _built = r)
            .Returns(new GraphRequest("q", new Dictionary<string, object?>()));
        _executor.Setup(e => e.ExecuteAsync(It.IsAny<GraphRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());
        _dialect.Setup(d => d.ParseSearch(It.IsAny<JsonElement>(), It.IsAny<bool>())).Returns(GraphSearchResult.Empty);
    }

    private GraphSearchService Create() => new(_dialect.Object, _executor.Object, _availability.Object, _types.Object);

    private static GraphSearchRequest Req(string phrase = "x", int limit = 10, int skip = 0,
        string[]? scopeTypes = null, string[]? filterTypes = null) =>
        new(phrase,
            new GraphSearchScope(scopeTypes ?? new[] { "PageData" }, Array.Empty<Guid>(), false),
            null,
            new GraphSearchFilters(filterTypes ?? Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Guid>()),
            limit, skip, false);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_phrase_returns_empty_without_calling_executor(string phrase)
    {
        var result = await Create().SearchAsync(Req(phrase), CancellationToken.None);
        result.Should().BeSameAs(GraphSearchResult.Empty);
        _executor.Verify(e => e.ExecuteAsync(It.IsAny<GraphRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unavailable_returns_empty_without_calling_executor()
    {
        _availability.SetupGet(a => a.IsAvailable).Returns(false);
        var result = await Create().SearchAsync(Req(), CancellationToken.None);
        result.Should().BeSameAs(GraphSearchResult.Empty);
        _executor.Verify(e => e.ExecuteAsync(It.IsAny<GraphRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ValidateContentTypes_keeps_only_registered_and_base_types_in_order()
    {
        Create().ValidateContentTypes(new[] { "ArticlePage", "Evil\") {", "PageData", "ArticlePage" })
            .Should().Equal("ArticlePage", "PageData");
    }

    [Fact]
    public void ValidateContentTypes_null_returns_empty() =>
        Create().ValidateContentTypes(null).Should().BeEmpty();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(10, 10)]
    [InlineData(9999, 100)] // Graph rejects limit > 100
    public async Task Limit_is_clamped(int limit, int expected)
    {
        await Create().SearchAsync(Req(limit: limit), CancellationToken.None);
        _built!.Limit.Should().Be(expected);
    }

    [Fact]
    public async Task Negative_skip_is_clamped_to_zero()
    {
        await Create().SearchAsync(Req(skip: -3), CancellationToken.None);
        _built!.Skip.Should().Be(0);
    }

    [Fact]
    public async Task Unvalidated_content_types_are_dropped_from_scope_and_filters()
    {
        await Create().SearchAsync(
            Req(scopeTypes: new[] { "PageData", "Evil\") {" }, filterTypes: new[] { "ArticlePage", "x\" y" }),
            CancellationToken.None);
        _built!.Scope.ContentTypes.Should().Equal("PageData");
        _built.Filters.ContentTypes.Should().Equal("ArticlePage");
    }
}
