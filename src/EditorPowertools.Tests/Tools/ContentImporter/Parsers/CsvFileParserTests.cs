using System.Text;
using EditorPowertools.Tools.ContentImporter.Parsers;
using FluentAssertions;

namespace EditorPowertools.Tests.Tools.ContentImporter.Parsers;

public class CsvFileParserTests
{
    private readonly CsvFileParser _parser = new();

    private static Stream ToStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    // --- CanParse ---

    [Theory]
    [InlineData(".csv", true)]
    [InlineData(".CSV", true)]
    [InlineData(".tsv", true)]
    [InlineData(".TSV", true)]
    [InlineData(".json", false)]
    [InlineData(".xlsx", false)]
    [InlineData(".txt", false)]
    public void CanParse_ReturnsExpectedResult(string extension, bool expected)
    {
        _parser.CanParse(extension).Should().Be(expected);
    }

    // --- Comma-delimited ---

    [Fact]
    public void Parse_CommaDelimited_ReturnsCorrectData()
    {
        var csv = "Name,Age,City\nAlice,30,Stockholm\nBob,25,Gothenburg";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "test.csv");

        result.Columns.Should().BeEquivalentTo(["Name", "Age", "City"]);
        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0]["Age"].Should().Be("30");
        result.Rows[0]["City"].Should().Be("Stockholm");
        result.Rows[1]["Name"].Should().Be("Bob");
        result.Rows[1]["City"].Should().Be("Gothenburg");
    }

    // --- Semicolon-delimited ---

    [Fact]
    public void Parse_SemicolonDelimited_ReturnsCorrectData()
    {
        var csv = "Name;Age;City\nAlice;30;Stockholm\nBob;25;Gothenburg";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "data.csv");

        result.Columns.Should().BeEquivalentTo(["Name", "Age", "City"]);
        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[1]["Age"].Should().Be("25");
    }

    // --- Tab-delimited (detected from content) ---

    [Fact]
    public void Parse_TabDelimitedCsv_ReturnsCorrectData()
    {
        var csv = "Name\tAge\tCity\nAlice\t30\tStockholm";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "data.csv");

        result.Columns.Should().BeEquivalentTo(["Name", "Age", "City"]);
        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Name"].Should().Be("Alice");
    }

    // --- Tab-delimited (.tsv extension) ---

    [Fact]
    public void Parse_TsvExtension_UsesTabDelimiter()
    {
        var tsv = "Name\tAge\nAlice\t30";
        using var stream = ToStream(tsv);

        var result = _parser.Parse(stream, "data.tsv");

        result.Columns.Should().BeEquivalentTo(["Name", "Age"]);
        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0]["Age"].Should().Be("30");
    }

    // --- Empty file ---

    [Fact]
    public void Parse_EmptyFile_ReturnsEmptyResult()
    {
        using var stream = ToStream("");

        var result = _parser.Parse(stream, "empty.csv");

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }

    // --- Header only ---

    [Fact]
    public void Parse_HeaderOnly_ReturnsColumnsButNoRows()
    {
        var csv = "Name,Age,City";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "headers.csv");

        result.Columns.Should().BeEquivalentTo(["Name", "Age", "City"]);
        result.Rows.Should().BeEmpty();
    }

    // --- Quoted fields with delimiters inside ---

    [Fact]
    public void Parse_QuotedFieldsContainingDelimiter_ParsesCorrectly()
    {
        var csv = "Name,Description,Value\n\"Smith, John\",\"A \"\"good\"\" item\",100";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "quoted.csv");

        result.Columns.Should().BeEquivalentTo(["Name", "Description", "Value"]);
        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Name"].Should().Be("Smith, John");
        result.Rows[0]["Description"].Should().Be("A \"good\" item");
        result.Rows[0]["Value"].Should().Be("100");
    }

    // --- Quoted fields with newlines ---

    [Fact]
    public void Parse_QuotedFieldsContainingNewline_ParsesCorrectly()
    {
        var csv = "Name,Description\n\"Alice\",\"Line1\nLine2\"";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "newline.csv");

        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Description"].Should().Be("Line1\nLine2");
    }

    // --- Whitespace trimming ---

    [Fact]
    public void Parse_TrimsWhitespace()
    {
        var csv = " Name , Age \n Alice , 30 ";
        using var stream = ToStream(csv);

        var result = _parser.Parse(stream, "trimmed.csv");

        result.Columns.Should().BeEquivalentTo(["Name", "Age"]);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0]["Age"].Should().Be("30");
    }
}
