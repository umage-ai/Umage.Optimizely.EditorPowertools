using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAbstraction.Activities;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActivityTimeline.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ActivityTimeline;

/// <summary>
/// Unit tests for ActivityTimelineService.
///
/// The multi-content path now goes through IActivityQueryService (the EPiServer
/// changelog) rather than GetDescendents + per-content version listing. The
/// EPiServer Activity / ContentActivity types are sealed-ish and hard to construct
/// from a unit test, so this file focuses on:
///   * the single-content path that uses IContentVersionRepository.List directly,
///   * CompareVersions,
///   * GetDistinctContentTypes,
///   * a smoke test that proves we never call GetDescendents.
/// Coverage of GetStats / GetDistinctUsers / multi-content GetActivities is
/// deferred to integration tests against a running CMS, since mocking the
/// changelog faithfully would require shipping fake ContentActivity instances.
/// </summary>
public class ActivityTimelineServiceTests
{
    private readonly Mock<IActivityQueryService> _activityQueryService = new();
    private readonly Mock<IContentVersionRepository> _versionRepo = new();
    private readonly Mock<IContentRepository> _contentRepo = new();
    private readonly Mock<IContentTypeRepository> _contentTypeRepo = new();
    private readonly Mock<ILogger<ActivityTimelineService>> _logger = new();

    private ActivityTimelineService CreateService()
    {
        // Default: empty changelog. Tests that exercise the changelog path can override.
        _activityQueryService
            .Setup(s => s.ListActivitiesAsync(It.IsAny<ActivityQuery>()))
            .ReturnsAsync(Enumerable.Empty<Activity>());

        return new ActivityTimelineService(
            _activityQueryService.Object,
            _versionRepo.Object,
            _contentRepo.Object,
            _contentTypeRepo.Object,
            _logger.Object);
    }

    private static ContentVersion MakeVersion(
        int contentId, int workId, VersionStatus status,
        DateTime saved, string savedBy, string? language = "en")
    {
        return new ContentVersion(
            new ContentReference(contentId, workId),
            $"Content {contentId}",
            status,
            saved,
            savedBy,
            savedBy,
            0,
            language ?? "en",
            false,
            true);
    }

    private void SetupVersionListExtension(params ContentVersion[] allVersions)
    {
        var totalCount = allVersions.Length;
        _versionRepo.Setup(r => r.List(
                It.IsAny<VersionFilter>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                out totalCount))
            .Returns(allVersions.AsEnumerable());
    }

    private void SetupContentTypes(params ContentType[] types)
    {
        _contentTypeRepo.Setup(r => r.List()).Returns(types);
    }

    private delegate void TryGetCallback(ContentReference contentRef, out IContent content);

    private void SetupContent(int contentId, string name, int contentTypeId)
    {
        var content = new Mock<IContent>();
        content.Setup(c => c.Name).Returns(name);
        content.Setup(c => c.ContentTypeID).Returns(contentTypeId);
        content.Setup(c => c.ContentLink).Returns(new ContentReference(contentId));

        _contentRepo.Setup(r => r.TryGet(
                It.Is<ContentReference>(cr => cr.ID == contentId),
                out It.Ref<IContent>.IsAny))
            .Callback(new TryGetCallback((ContentReference cr, out IContent c) => c = content.Object))
            .Returns(true);
    }

    // ===================================================================
    // GetActivities never calls GetDescendents — that's the contract
    // ===================================================================

    [Fact]
    public void GetActivities_NeverCallsGetDescendents()
    {
        SetupVersionListExtension();
        SetupContentTypes();

        var svc = CreateService();
        svc.GetActivities(new ActivityFilterRequest { Take = 50 });
        svc.GetActivities(new ActivityFilterRequest { ContentId = 42, Take = 50 });

        _contentRepo.Verify(r => r.GetDescendents(It.IsAny<ContentReference>()), Times.Never);
    }

    [Fact]
    public void GetStats_NeverCallsGetDescendents()
    {
        var svc = CreateService();
        svc.GetStats();

        _contentRepo.Verify(r => r.GetDescendents(It.IsAny<ContentReference>()), Times.Never);
    }

    [Fact]
    public void GetDistinctUsers_NeverCallsGetDescendents()
    {
        var svc = CreateService();
        svc.GetDistinctUsers();

        _contentRepo.Verify(r => r.GetDescendents(It.IsAny<ContentReference>()), Times.Never);
    }

    // ===================================================================
    // Single-content path
    // ===================================================================

    [Fact]
    public void GetActivities_FilterByContentId_PopulatesContentName()
    {
        var v1 = MakeVersion(42, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionListExtension(v1);
        SetupContentTypes();
        SetupContent(42, "My Special Page", 0);

        var svc = CreateService();
        var result = svc.GetActivities(new ActivityFilterRequest
        {
            ContentId = 42,
            Action = "Published",
            Take = 50
        });

        result.ContentName.Should().Be("My Special Page");
    }

    // ===================================================================
    // CompareVersions
    // ===================================================================

    [Fact]
    public void CompareVersions_ReturnsPropertyChanges()
    {
        var contentRef = new ContentReference(10, 2);
        var prevRef = new ContentReference(10, 1);

        var currentContent = new Mock<IContent>();
        currentContent.Setup(c => c.Name).Returns("Test Page");

        var currentProps = new PropertyDataCollection();
        var currentTitle = new PropertyString("New Title") { Name = "Title" };
        var currentBody = new PropertyString("Same body") { Name = "Body" };
        currentProps.Add(currentTitle);
        currentProps.Add(currentBody);
        currentContent.Setup(c => c.Property).Returns(currentProps);

        _contentRepo.Setup(r => r.Get<IContent>(contentRef)).Returns(currentContent.Object);

        var previousContent = new Mock<IContent>();
        previousContent.Setup(c => c.Name).Returns("Test Page");

        var prevProps = new PropertyDataCollection();
        var prevTitle = new PropertyString("Old Title") { Name = "Title" };
        var prevBody = new PropertyString("Same body") { Name = "Body" };
        prevProps.Add(prevTitle);
        prevProps.Add(prevBody);
        previousContent.Setup(c => c.Property).Returns(prevProps);

        _contentRepo.Setup(r => r.Get<IContent>(prevRef)).Returns(previousContent.Object);

        var currentVersion = MakeVersion(10, 2, VersionStatus.Published, new DateTime(2025, 2, 1), "alice");
        var previousVersion = MakeVersion(10, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionListExtension(currentVersion, previousVersion);

        var svc = CreateService();
        var result = svc.CompareVersions(10, 2, "en");

        result.HasPrevious.Should().BeTrue();
        result.ContentName.Should().Be("Test Page");
        result.CurrentVersion.Should().Be(2);
        result.PreviousVersion.Should().Be(1);
        result.Changes.Should().HaveCount(1);
        result.Changes[0].PropertyName.Should().Be("Title");
        result.Changes[0].OldValue.Should().Be("Old Title");
        result.Changes[0].NewValue.Should().Be("New Title");
    }

    [Fact]
    public void CompareVersions_NoPreviousVersion_ReturnsFalse()
    {
        var contentRef = new ContentReference(10, 1);

        var content = new Mock<IContent>();
        content.Setup(c => c.Name).Returns("Test Page");
        content.Setup(c => c.Property).Returns(new PropertyDataCollection());
        _contentRepo.Setup(r => r.Get<IContent>(contentRef)).Returns(content.Object);

        var version = MakeVersion(10, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionListExtension(version);

        var svc = CreateService();
        var result = svc.CompareVersions(10, 1, "en");

        result.HasPrevious.Should().BeFalse();
        result.ContentName.Should().Be("Test Page");
    }

    [Fact]
    public void CompareVersions_NoChanges_ReturnsEmptyChangeList()
    {
        var contentRef = new ContentReference(10, 2);
        var prevRef = new ContentReference(10, 1);

        var currentContent = new Mock<IContent>();
        currentContent.Setup(c => c.Name).Returns("Test Page");
        var currentProps = new PropertyDataCollection();
        var currentTitle = new PropertyString("Same Title") { Name = "Title" };
        currentProps.Add(currentTitle);
        currentContent.Setup(c => c.Property).Returns(currentProps);
        _contentRepo.Setup(r => r.Get<IContent>(contentRef)).Returns(currentContent.Object);

        var previousContent = new Mock<IContent>();
        previousContent.Setup(c => c.Name).Returns("Test Page");
        var prevProps = new PropertyDataCollection();
        var prevTitle = new PropertyString("Same Title") { Name = "Title" };
        prevProps.Add(prevTitle);
        previousContent.Setup(c => c.Property).Returns(prevProps);
        _contentRepo.Setup(r => r.Get<IContent>(prevRef)).Returns(previousContent.Object);

        var v1 = MakeVersion(10, 2, VersionStatus.Published, new DateTime(2025, 2, 1), "alice");
        var v2 = MakeVersion(10, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionListExtension(v1, v2);

        var svc = CreateService();
        var result = svc.CompareVersions(10, 2, "en");

        result.HasPrevious.Should().BeTrue();
        result.Changes.Should().BeEmpty();
    }

    // ===================================================================
    // GetDistinctContentTypes
    // ===================================================================

    [Fact]
    public void GetDistinctContentTypes_ReturnsSortedNames()
    {
        var types = new[]
        {
            new ContentType { Name = "StandardPage", DisplayName = "Standard Page" },
            new ContentType { Name = "ArticlePage", DisplayName = "Article Page" },
            new ContentType { Name = "BlockBase", DisplayName = null },
        };
        SetupContentTypes(types);

        var svc = CreateService();
        var result = svc.GetDistinctContentTypes().ToList();

        result.Should().BeEquivalentTo(new[] { "Article Page", "BlockBase", "Standard Page" });
        result.Should().BeInAscendingOrder();
    }
}
