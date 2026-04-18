using UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Parsers;
using FluentAssertions;
using ClosedXML.Excel;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ContentImporter.Parsers;

public class ExcelFileParserTests
{
    private readonly ExcelFileParser _parser = new();

    private static Stream CreateExcelStream(Action<IXLWorksheet> configure)
    {
        var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Sheet1");
            configure(ws);
            wb.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }

    private static Stream CreateEmptyExcelStream()
    {
        var stream = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            wb.Worksheets.Add("Sheet1");
            wb.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }

    // --- CanParse ---

    [Theory]
    [InlineData(".xlsx", true)]
    [InlineData(".XLSX", true)]
    [InlineData(".xls", true)]
    [InlineData(".XLS", true)]
    [InlineData(".csv", false)]
    [InlineData(".json", false)]
    [InlineData(".txt", false)]
    public void CanParse_ReturnsExpectedResult(string extension, bool expected)
    {
        _parser.CanParse(extension).Should().Be(expected);
    }

    // --- Basic parsing ---

    [Fact]
    public void Parse_BasicWorksheet_ReturnsCorrectData()
    {
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Age";
            ws.Cell(1, 3).Value = "City";
            ws.Cell(2, 1).Value = "Alice";
            ws.Cell(2, 2).Value = 30;
            ws.Cell(2, 3).Value = "Stockholm";
            ws.Cell(3, 1).Value = "Bob";
            ws.Cell(3, 2).Value = 25;
            ws.Cell(3, 3).Value = "Gothenburg";
        });

        var result = _parser.Parse(stream, "test.xlsx");

        result.Columns.Should().BeEquivalentTo(["Name", "Age", "City"]);
        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[0]["Age"].Should().Be("30");
        result.Rows[0]["City"].Should().Be("Stockholm");
        result.Rows[1]["Name"].Should().Be("Bob");
    }

    // --- Empty worksheet (no data) ---

    [Fact]
    public void Parse_EmptyWorksheet_ReturnsEmptyResult()
    {
        using var stream = CreateEmptyExcelStream();

        var result = _parser.Parse(stream, "empty.xlsx");

        result.Columns.Should().BeEmpty();
        result.Rows.Should().BeEmpty();
    }

    // --- Header only ---

    [Fact]
    public void Parse_HeaderOnly_ReturnsColumnsButNoRows()
    {
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Age";
        });

        var result = _parser.Parse(stream, "headeronly.xlsx");

        result.Columns.Should().BeEquivalentTo(["Name", "Age"]);
        result.Rows.Should().BeEmpty();
    }

    // --- Empty header cells get default names ---

    [Fact]
    public void Parse_EmptyHeaderCell_GetsDefaultColumnName()
    {
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            // Column 2 header intentionally left blank
            ws.Cell(1, 3).Value = "City";
            ws.Cell(2, 1).Value = "Alice";
            ws.Cell(2, 2).Value = "Extra";
            ws.Cell(2, 3).Value = "Stockholm";
        });

        var result = _parser.Parse(stream, "defaults.xlsx");

        result.Columns.Should().Contain("Column2");
        result.Rows[0]["Column2"].Should().Be("Extra");
    }

    // --- Rows with all empty cells are skipped ---

    [Fact]
    public void Parse_EmptyDataRow_IsSkipped()
    {
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "Age";
            ws.Cell(2, 1).Value = "Alice";
            ws.Cell(2, 2).Value = 30;
            // Row 3 is completely empty but within dimension due to row 4
            ws.Cell(4, 1).Value = "Bob";
            ws.Cell(4, 2).Value = 25;
        });

        var result = _parser.Parse(stream, "gaps.xlsx");

        result.Rows.Should().HaveCount(2);
        result.Rows[0]["Name"].Should().Be("Alice");
        result.Rows[1]["Name"].Should().Be("Bob");
    }

    // --- Numeric and date values converted to text ---

    [Fact]
    public void Parse_NumericValues_ConvertedToText()
    {
        using var stream = CreateExcelStream(ws =>
        {
            ws.Cell(1, 1).Value = "Value";
            ws.Cell(2, 1).Value = 42.5;
        });

        var result = _parser.Parse(stream, "numbers.xlsx");

        result.Rows.Should().HaveCount(1);
        result.Rows[0]["Value"].Should().Be("42.5");
    }
}
