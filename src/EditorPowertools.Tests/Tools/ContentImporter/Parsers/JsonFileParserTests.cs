using System.Text;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Parsers;
using FluentAssertions;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ContentImporter.Parsers;

public class JsonFileParserTests
{
    private readonly JsonFileParser _parser = new();

    private static Stream ToStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    // --- CanParse ---

    [Theory]
    [InlineData(".json", true)]
    [InlineData(".JSON", true)]
    [InlineData(".csv", false)]
    [InlineData(".xlsx", false)]
    [InlineData(".xml", false)]
    public void CanParse_ReturnsExpectedResult(string extension, bool expected)
    {
        _parser.CanParse(extension).Should().Be(expected);
    }

    // --- Array of objects ---

    [Fact]
    public void Parse_ArrayOfObjects_ReturnsCorrectData()
    {
        var json = """
            [
                { "Name": "Alice", "Age": 30 },
                { "Name": "Bob", "Age": 25 }
            ]
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "data.json");

        result.Columns.Should().BeEquivalentTo(["Name", "Age"]);
        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0]["Age"].Should().Be("30");
        result.Rows[1]["Name"].Should().Be("Bob");
        result.Rows[1]["Age"].Should().Be("25");
    }

    // --- Wrapped array ---

    [Fact]
    public void Parse_WrappedArray_ReturnsCorrectData()
    {
        var json = """
            {
                "data": [
                    { "Name": "Alice", "City": "Stockholm" },
                    { "Name": "Bob", "City": "Gothenburg" }
                ]
            }
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "wrapped.json");

        result.Columns.Should().BeEquivalentTo(["Name", "City"]);
        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[1]["City"].Should().Be("Gothenburg");
    }

    // --- Flattens nested objects with dot notation ---

    [Fact]
    public void Parse_NestedObjects_FlattensWithDotNotation()
    {
        var json = """
            [
                {
                    "Name": "Alice",
                    "Address": {
                        "City": "Stockholm",
                        "Country": "Sweden"
                    }
                }
            ]
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "nested.json");

        result.Columns.Should().Contain("Address.City");
        result.Columns.Should().Contain("Address.Country");
        result.Rows[0]["Address.City"].Should().Be("Stockholm");
        result.Rows[0]["Address.Country"].Should().Be("Sweden");
    }

    // --- Deeply nested objects ---

    [Fact]
    public void Parse_DeeplyNestedObjects_FlattensRecursively()
    {
        var json = """
            [
                {
                    "Person": {
                        "Name": "Alice",
                        "Address": {
                            "City": "Stockholm"
                        }
                    }
                }
            ]
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "deep.json");

        result.Columns.Should().Contain("Person.Name");
        result.Columns.Should().Contain("Person.Address.City");
        result.Rows[0]["Person.Address.City"].Should().Be("Stockholm");
    }

    // --- Empty array ---

    [Fact]
    public void Parse_EmptyArray_ReturnsEmptyResult()
    {
        var json = "[]";
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "empty.json");

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }

    // --- Empty wrapped array ---

    [Fact]
    public void Parse_EmptyWrappedArray_ReturnsEmptyResult()
    {
        var json = """{ "data": [] }""";
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "empty-wrapped.json");

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }

    // --- Null values become empty strings ---

    [Fact]
    public void Parse_NullValues_BecomesEmptyString()
    {
        var json = """
            [
                { "Name": "Alice", "Email": null }
            ]
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "nulls.json");

        result.Rows[0]["Email"].Should().Be("");
    }

    // --- Array values preserved as raw JSON ---

    [Fact]
    public void Parse_ArrayValues_PreservedAsRawJson()
    {
        var json = """
            [
                { "Name": "Alice", "Tags": ["admin", "editor"] }
            ]
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "arrays.json");

        result.Columns.Should().Contain("Tags");
        result.Rows[0]["Tags"].Should().Contain("admin");
        result.Rows[0]["Tags"].Should().Contain("editor");
    }

    // --- Object without array property ---

    [Fact]
    public void Parse_ObjectWithoutArrayProperty_ReturnsEmptyResult()
    {
        var json = """{ "name": "test", "value": 42 }""";
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "noarray.json");

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }

    // --- Heterogeneous rows (different columns per object) ---

    [Fact]
    public void Parse_HeterogeneousRows_CollectsAllColumns()
    {
        var json = """
            [
                { "Name": "Alice", "Age": 30 },
                { "Name": "Bob", "City": "Stockholm" }
            ]
            """;
        using var stream = ToStream(json);

        var result = _parser.Parse(stream, "hetero.json");

        result.Columns.Should().Contain("Name");
        result.Columns.Should().Contain("Age");
        result.Columns.Should().Contain("City");
        result.Rows.Should().HaveCount(2);
    }
}
