using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.Diagnostics.Checks;
using UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview.Models;
using UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;
using FluentAssertions;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Tools.Diagnostics;

/// <summary>
/// Tests the four Forms CMS Doctor checks. Each check takes only an
/// <see cref="IFormsAggregationService"/> (mocked here), and produces a
/// <see cref="DoctorCheckResult"/> whose <see cref="DoctorCheckResult.Status"/>
/// reflects whether a problem was found. The base class resolves a
/// <see cref="EPiServer.Framework.Localization.LocalizationService"/> from the
/// ServiceLocator when building the result, so the fixture wires up a minimal one.
/// </summary>
public class FormsDoctorChecksTests
{
    private readonly Mock<IFormsAggregationService> _forms = new();

    public FormsDoctorChecksTests()
    {
        FormsTestSetup.EnsureInitialized();

        // Sensible "nothing wrong" defaults; individual tests override as needed.
        _forms.Setup(f => f.GetForms()).Returns(Array.Empty<FormSummaryDto>());
        _forms.Setup(f => f.AnalyzePii()).Returns(Array.Empty<FormPiiAnalysisDto>());
    }

    private static FormSummaryDto Form(
        string name = "Contact",
        int usageCount = 1,
        int submissionCount = 0,
        bool hasEmailHandler = false,
        bool hasWebhookHandler = false,
        IEnumerable<string>? duplicateFieldLabels = null)
        => new()
        {
            Name = name,
            UsageCount = usageCount,
            SubmissionCount = submissionCount,
            HasEmailHandler = hasEmailHandler,
            HasWebhookHandler = hasWebhookHandler,
            DuplicateFieldLabels = duplicateFieldLabels?.ToList() ?? new List<string>()
        };

    // ---------- UnusedFormsCheck ----------

    [Fact]
    public void UnusedFormsCheck_FlagsFormsWithNoUsage()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[]
        {
            Form("Unused A", usageCount: 0),
            Form("Used B", usageCount: 3)
        });

        var result = new UnusedFormsCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.BadPractice);
        result.StatusText.Should().Contain("1");
    }

    [Fact]
    public void UnusedFormsCheck_OkWhenAllFormsAreUsed()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[] { Form(usageCount: 2) });

        var result = new UnusedFormsCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.OK);
    }

    // ---------- NoNotificationHandlerCheck ----------

    [Fact]
    public void NoNotificationHandlerCheck_FlagsFormWithSubmissionsButNoHandler()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[]
        {
            Form("Orphan", submissionCount: 5, hasEmailHandler: false, hasWebhookHandler: false)
        });

        var result = new NoNotificationHandlerCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.Warning);
    }

    [Fact]
    public void NoNotificationHandlerCheck_OkWhenFormWithSubmissionsHasEmailHandler()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[]
        {
            Form("Handled", submissionCount: 5, hasEmailHandler: true)
        });

        var result = new NoNotificationHandlerCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.OK);
    }

    [Fact]
    public void NoNotificationHandlerCheck_OkWhenFormHasNoSubmissions()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[]
        {
            Form("Idle", submissionCount: 0, hasEmailHandler: false, hasWebhookHandler: false)
        });

        var result = new NoNotificationHandlerCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.OK);
    }

    // ---------- PiiIndefiniteRetentionCheck ----------

    [Fact]
    public void PiiIndefiniteRetentionCheck_FlagsPiiOnDefaultRetentionThatStoresData()
    {
        _forms.Setup(f => f.AnalyzePii()).Returns(new[]
        {
            new FormPiiAnalysisDto
            {
                FormName = "Newsletter",
                PiiFieldLabels = { "Email", "Name" },
                UsesDefaultRetention = true,
                StoresSubmissionData = true
            }
        });

        var result = new PiiIndefiniteRetentionCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.Warning);
    }

    [Theory]
    // no PII labels
    [InlineData(false, true, true)]
    // not on default retention
    [InlineData(true, false, true)]
    // does not store submission data
    [InlineData(true, true, false)]
    public void PiiIndefiniteRetentionCheck_OkWhenAnyConditionMissing(
        bool hasPii, bool defaultRetention, bool storesData)
    {
        var dto = new FormPiiAnalysisDto
        {
            FormName = "Form",
            UsesDefaultRetention = defaultRetention,
            StoresSubmissionData = storesData
        };
        if (hasPii) dto.PiiFieldLabels.Add("Email");

        _forms.Setup(f => f.AnalyzePii()).Returns(new[] { dto });

        var result = new PiiIndefiniteRetentionCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.OK);
    }

    // ---------- DuplicateFieldsCheck ----------

    [Fact]
    public void DuplicateFieldsCheck_FlagsFormWithDuplicateLabels()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[]
        {
            Form("Survey", duplicateFieldLabels: new[] { "Email" })
        });

        var result = new DuplicateFieldsCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.Warning);
    }

    [Fact]
    public void DuplicateFieldsCheck_OkWhenNoFormHasDuplicateLabels()
    {
        _forms.Setup(f => f.GetForms()).Returns(new[]
        {
            Form("Clean", duplicateFieldLabels: Array.Empty<string>())
        });

        var result = new DuplicateFieldsCheck(_forms.Object).PerformCheck();

        result.Status.Should().Be(HealthStatus.OK);
    }
}
