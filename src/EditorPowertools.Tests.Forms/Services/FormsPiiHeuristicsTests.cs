using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using FluentAssertions;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Services;

/// <summary>
/// Unit tests for the internal PII detection heuristic. Visible to this project
/// via <c>[InternalsVisibleTo("EditorPowertools.Tests.Forms")]</c> on the Forms assembly.
/// </summary>
public class FormsPiiHeuristicsTests
{
    [Fact]
    public void LooksLikePii_FileUploadElementType_ReturnsTrueWithFileUploadHint()
    {
        var result = FormsPiiHeuristics.LooksLikePii("FileUploadElementBlock", label: null, out var hint);

        result.Should().BeTrue();
        hint.Should().Be("file upload");
    }

    [Fact]
    public void LooksLikePii_FileUploadElementType_IsCaseInsensitiveOnType()
    {
        var result = FormsPiiHeuristics.LooksLikePii("fileuploadELEMENTblock", label: null, out var hint);

        result.Should().BeTrue();
        hint.Should().Be("file upload");
    }

    [Theory]
    [InlineData("Email address", "email")]
    [InlineData("Your e-mail", "e-mail")]
    [InlineData("Phone", "phone")]
    [InlineData("Home address", "address")]
    [InlineData("navn", "navn")]          // Danish/Norwegian "name"
    [InlineData("Telefon", "tel")]        // Danish "phone" — matches the earlier "tel" keyword first
    public void LooksLikePii_LabelWithKeyword_ReturnsTrueWithMatchedKeywordHint(string label, string expectedHint)
    {
        var result = FormsPiiHeuristics.LooksLikePii(elementTypeName: null, label, out var hint);

        result.Should().BeTrue();
        hint.Should().Be(expectedHint);
    }

    [Theory]
    [InlineData("EMAIL ADDRESS")]
    [InlineData("Email Address")]
    [InlineData("eMaIl")]
    public void LooksLikePii_LabelMatching_IsCaseInsensitive(string label)
    {
        var result = FormsPiiHeuristics.LooksLikePii(elementTypeName: null, label, out var hint);

        result.Should().BeTrue();
        hint.Should().Be("email");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("", null)]
    public void LooksLikePii_NullOrEmptyTypeAndLabel_ReturnsFalseWithEmptyHint(string? type, string? label)
    {
        var result = FormsPiiHeuristics.LooksLikePii(type, label, out var hint);

        result.Should().BeFalse();
        hint.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Favourite colour")]
    [InlineData("Rating")]
    [InlineData("Comments")]
    public void LooksLikePii_LabelWithoutKeyword_ReturnsFalseWithEmptyHint(string label)
    {
        var result = FormsPiiHeuristics.LooksLikePii(elementTypeName: null, label, out var hint);

        result.Should().BeFalse();
        hint.Should().BeEmpty();
    }

    [Fact]
    public void LooksLikePii_NonPiiElementType_FallsBackToLabelInspection()
    {
        // A textbox element with a non-PII label is not flagged...
        FormsPiiHeuristics.LooksLikePii("TextboxElementBlock", "Favourite colour", out _)
            .Should().BeFalse();

        // ...but the same element type with a PII label still is.
        FormsPiiHeuristics.LooksLikePii("TextboxElementBlock", "Email", out var hint)
            .Should().BeTrue();
        hint.Should().Be("email");
    }
}
