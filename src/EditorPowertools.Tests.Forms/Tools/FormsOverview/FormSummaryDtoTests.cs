using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;
using FluentAssertions;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Tools.FormsOverview;

/// <summary>
/// Tests for the computed risk/quality flags on <see cref="FormSummaryDto"/>.
/// These are pure logic with no EPiServer dependency.
/// </summary>
public class FormSummaryDtoTests
{
    [Fact]
    public void HasDuplicateFields_TrueWhenLabelsPresent_FalseWhenEmpty()
    {
        new FormSummaryDto().HasDuplicateFields.Should().BeFalse();

        new FormSummaryDto { DuplicateFieldLabels = { "Email" } }
            .HasDuplicateFields.Should().BeTrue();
    }

    [Fact]
    public void CapturesPii_TrueWhenPiiLabelsPresent_FalseWhenEmpty()
    {
        new FormSummaryDto().CapturesPii.Should().BeFalse();

        new FormSummaryDto { PiiFieldLabels = { "Email" } }
            .CapturesPii.Should().BeTrue();
    }

    // PrivacyRisk == CapturesPii && StoresSubmissionData && UsesDefaultRetention
    [Theory]
    [InlineData(true, true, true, true)]    // all three -> risk
    [InlineData(false, true, true, false)]  // no PII
    [InlineData(true, false, true, false)]  // does not store data
    [InlineData(true, true, false, false)]  // not on default retention
    [InlineData(false, false, false, false)]
    public void PrivacyRisk_RequiresPiiAndStorageAndDefaultRetention(
        bool capturesPii, bool storesData, bool defaultRetention, bool expected)
    {
        var dto = new FormSummaryDto
        {
            StoresSubmissionData = storesData,
            UsesDefaultRetention = defaultRetention
        };
        if (capturesPii) dto.PiiFieldLabels.Add("Email");

        dto.PrivacyRisk.Should().Be(expected);
    }

    // PrivacyRiskIsLive == PrivacyRisk && IsPublished && SubmissionCount > 0
    [Theory]
    [InlineData(true, true, 1, true)]    // privacy risk + published + has submissions -> live
    [InlineData(true, false, 1, false)] // not published
    [InlineData(true, true, 0, false)]  // no submissions
    [InlineData(false, true, 1, false)] // not a privacy risk (storesData false below)
    public void PrivacyRiskIsLive_RequiresRiskPublishedAndSubmissions(
        bool isPrivacyRisk, bool isPublished, int submissionCount, bool expected)
    {
        var dto = new FormSummaryDto
        {
            // Drive PrivacyRisk via these three; toggle storesData to flip the risk off.
            PiiFieldLabels = { "Email" },
            StoresSubmissionData = isPrivacyRisk,
            UsesDefaultRetention = true,
            IsPublished = isPublished,
            SubmissionCount = submissionCount
        };

        dto.PrivacyRisk.Should().Be(isPrivacyRisk);
        dto.PrivacyRiskIsLive.Should().Be(expected);
    }
}
