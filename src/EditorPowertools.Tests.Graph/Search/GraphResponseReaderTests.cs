using System.Text.Json;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace EditorPowertools.Tests.Graph.Search;

public class GraphResponseReaderTests
{
    [Fact]
    public void Returns_cloned_data_element() =>
        GraphResponseReader.ReadData("""{"data":{"Content":{"total":3}}}""")
            .GetProperty("Content").GetProperty("total").GetInt32().Should().Be(3);

    [Fact]
    public void Throws_on_graphql_errors_without_leaking_to_callers()
    {
        var act = () => GraphResponseReader.ReadData("""{"errors":[{"message":"Field 'X' not found"}]}""");
        act.Should().Throw<GraphQueryException>().Which.Message.Should().Contain("Field 'X' not found");
    }

    [Fact]
    public void Throws_on_invalid_json() =>
        ((Action)(() => GraphResponseReader.ReadData("<html>"))).Should().Throw<GraphQueryException>();

    [Fact]
    public void Missing_data_returns_undefined_element() =>
        GraphResponseReader.ReadData("{}").ValueKind.Should().Be(JsonValueKind.Undefined);
}
