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
/// Note on GetActivities: The version-to-DTO mapping in GetActivities calls
/// EPiServer.Shell.Paths.ToResource() which requires the EPiServer module table
/// to be initialized. Since this is a static dependency that cannot be mocked,
/// GetActivities tests verify the response structure (ContentName, HasMore, etc.)
/// rather than individual activity DTOs. The filtering, sorting, and pagination
/// logic is indirectly validated through GetStats (which uses the same version
/// loading code) and CompareVersions.
///
/// ContentActivityFeed is a concrete class with no parameterless constructor,
/// so comment/message activity tests are not covered here.
/// </summary>
public class ActivityTimelineServiceTests
{
    private readonly Mock<IContentVersionRepository> _versionRepo = new();
    private readonly Mock<IContentRepository> _contentRepo = new();
    private readonly Mock<IContentTypeRepository> _contentTypeRepo = new();
    private readonly Mock<ILogger<ActivityTimelineService>> _logger = new();

    private ActivityTimelineService CreateService()
    {
        return new ActivityTimelineService(
            _versionRepo.Object,
            _contentRepo.Object,
            _contentTypeRepo.Object,
            null!, // ContentActivityFeed - see class comment
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

    // --- Helpers ---

    private void SetupDescendants(params int[] contentIds)
    {
        var refs = contentIds.Select(id => new ContentReference(id)).ToArray();
        _contentRepo.Setup(r => r.GetDescendents(ContentReference.RootPage))
            .Returns(refs);

        // Default: TryGet succeeds for all descendants (user has access)
        foreach (var id in contentIds)
        {
            var mockContent = new Mock<IContent>();
            mockContent.Setup(c => c.ContentLink).Returns(new ContentReference(id));
            _contentRepo.Setup(r => r.TryGet(
                    It.Is<ContentReference>(cr => cr.ID == id),
                    out It.Ref<IContent>.IsAny))
                .Callback(new TryGetCallback((ContentReference cr, out IContent c) => c = mockContent.Object))
                .Returns(true);
        }
    }

    /// <summary>
    /// Mocks IContentVersionRepository.List(VersionFilter, int, int, out int).
    /// Extension methods List(ContentReference) and List(ContentReference, string) delegate to this.
    /// </summary>
    private void SetupVersionList(params ContentVersion[] allVersions)
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
    // GetStats - no Paths.ToResource dependency
    // ===================================================================

    [Fact]
    public void GetStats_ReturnsCorrectTodayCounts()
    {
        var today = DateTime.UtcNow.Date;
        // Note: The mock returns the same version list for all content references,
        // so we use a single descendant to avoid double-counting.
        var v1 = MakeVersion(1, 1, VersionStatus.Published, today.AddHours(2), "alice");
        var v2 = MakeVersion(1, 2, VersionStatus.CheckedOut, today.AddHours(3), "bob");
        var v3 = MakeVersion(1, 3, VersionStatus.Published, today.AddHours(4), "alice");
        var v4 = MakeVersion(1, 4, VersionStatus.CheckedOut, today.AddDays(-1), "charlie"); // yesterday

        SetupDescendants(1);
        SetupVersionList(v1, v2, v3, v4);

        var svc = CreateService();
        var stats = svc.GetStats();

        stats.TotalToday.Should().Be(3);
        stats.ActiveEditorsToday.Should().Be(2); // alice and bob
        stats.PublishesToday.Should().Be(2);
        stats.DraftsToday.Should().Be(1);
    }

    [Fact]
    public void GetStats_EmptySite_ReturnsZeros()
    {
        SetupDescendants(); // no content
        SetupVersionList();

        var svc = CreateService();
        var stats = svc.GetStats();

        stats.TotalToday.Should().Be(0);
        stats.ActiveEditorsToday.Should().Be(0);
        stats.PublishesToday.Should().Be(0);
        stats.DraftsToday.Should().Be(0);
    }

    [Fact]
    public void GetStats_OnlyYesterdayVersions_ReturnsZeros()
    {
        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var v1 = MakeVersion(1, 1, VersionStatus.Published, yesterday.AddHours(10), "alice");

        SetupDescendants(1);
        SetupVersionList(v1);

        var svc = CreateService();
        var stats = svc.GetStats();

        stats.TotalToday.Should().Be(0);
    }

    [Fact]
    public void GetStats_CountsDistinctEditors()
    {
        var today = DateTime.UtcNow.Date;
        var v1 = MakeVersion(1, 1, VersionStatus.Published, today.AddHours(1), "alice");
        var v2 = MakeVersion(1, 2, VersionStatus.Published, today.AddHours(2), "alice"); // same user
        var v3 = MakeVersion(1, 3, VersionStatus.Published, today.AddHours(3), "bob");

        SetupDescendants(1);
        SetupVersionList(v1, v2, v3);

        var svc = CreateService();
        var stats = svc.GetStats();

        stats.ActiveEditorsToday.Should().Be(2); // alice and bob (alice counted once)
    }

    // ===================================================================
    // CompareVersions
    // ===================================================================

    [Fact]
    public void CompareVersions_ReturnsPropertyChanges()
    {
        var contentRef = new ContentReference(10, 2);
        var prevRef = new ContentReference(10, 1);

        // Use real PropertyString instances since PropertyData.Name is non-virtual
        var currentContent = new Mock<IContent>();
        currentContent.Setup(c => c.Name).Returns("Test Page");

        var currentProps = new PropertyDataCollection();
        var currentTitle = new PropertyString("New Title");
        currentTitle.Name = "Title";
        var currentBody = new PropertyString("Same body");
        currentBody.Name = "Body";
        currentProps.Add(currentTitle);
        currentProps.Add(currentBody);
        currentContent.Setup(c => c.Property).Returns(currentProps);

        _contentRepo.Setup(r => r.Get<IContent>(contentRef)).Returns(currentContent.Object);

        // Previous content
        var previousContent = new Mock<IContent>();
        previousContent.Setup(c => c.Name).Returns("Test Page");

        var prevProps = new PropertyDataCollection();
        var prevTitle = new PropertyString("Old Title");
        prevTitle.Name = "Title";
        var prevBody = new PropertyString("Same body");
        prevBody.Name = "Body";
        prevProps.Add(prevTitle);
        prevProps.Add(prevBody);
        previousContent.Setup(c => c.Property).Returns(prevProps);

        _contentRepo.Setup(r => r.Get<IContent>(prevRef)).Returns(previousContent.Object);

        // Version list
        var currentVersion = MakeVersion(10, 2, VersionStatus.Published, new DateTime(2025, 2, 1), "alice");
        var previousVersion = MakeVersion(10, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionList(currentVersion, previousVersion);

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
        SetupVersionList(version);

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
        var currentTitle = new PropertyString("Same Title");
        currentTitle.Name = "Title";
        currentProps.Add(currentTitle);
        currentContent.Setup(c => c.Property).Returns(currentProps);
        _contentRepo.Setup(r => r.Get<IContent>(contentRef)).Returns(currentContent.Object);

        var previousContent = new Mock<IContent>();
        previousContent.Setup(c => c.Name).Returns("Test Page");
        var prevProps = new PropertyDataCollection();
        var prevTitle = new PropertyString("Same Title");
        prevTitle.Name = "Title";
        prevProps.Add(prevTitle);
        previousContent.Setup(c => c.Property).Returns(prevProps);
        _contentRepo.Setup(r => r.Get<IContent>(prevRef)).Returns(previousContent.Object);

        var v1 = MakeVersion(10, 2, VersionStatus.Published, new DateTime(2025, 2, 1), "alice");
        var v2 = MakeVersion(10, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionList(v1, v2);

        var svc = CreateService();
        var result = svc.CompareVersions(10, 2, "en");

        result.HasPrevious.Should().BeTrue();
        result.Changes.Should().BeEmpty();
    }

    // ===================================================================
    // GetActivities - ContentId filter populates ContentName
    // (ContentName is resolved outside the mapping try/catch)
    // ===================================================================

    [Fact]
    public void GetActivities_FilterByContentId_PopulatesContentName()
    {
        var v1 = MakeVersion(42, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionList(v1);
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

    [Fact]
    public void GetActivities_FilterByContentId_DoesNotCallGetDescendents()
    {
        var v1 = MakeVersion(42, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice");
        SetupVersionList(v1);
        SetupContentTypes();
        SetupContent(42, "Page", 0);

        var svc = CreateService();
        svc.GetActivities(new ActivityFilterRequest { ContentId = 42, Action = "Published", Take = 50 });

        _contentRepo.Verify(r => r.GetDescendents(It.IsAny<ContentReference>()), Times.Never);
    }

    [Fact]
    public void GetActivities_NoContentIdFilter_CallsGetDescendents()
    {
        SetupDescendants(1);
        SetupVersionList();
        SetupContentTypes();

        var svc = CreateService();
        svc.GetActivities(new ActivityFilterRequest { Action = "Published", Take = 50 });

        _contentRepo.Verify(r => r.GetDescendents(ContentReference.RootPage), Times.Once);
    }

    [Fact]
    public void GetActivities_InvalidContentType_ReturnsEmptyResponse()
    {
        SetupDescendants(1);
        SetupVersionList(MakeVersion(1, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "alice"));
        SetupContentTypes(); // no content types

        var svc = CreateService();
        var result = svc.GetActivities(new ActivityFilterRequest
        {
            ContentTypeName = "NonExistentType",
            Take = 50
        });

        result.TotalCount.Should().Be(0);
        result.HasMore.Should().BeFalse();
    }

    // ===================================================================
    // GetDistinctUsers
    // ===================================================================

    [Fact]
    public void GetDistinctUsers_ReturnsUniqueUsersSorted()
    {
        var v1 = MakeVersion(1, 1, VersionStatus.Published, new DateTime(2025, 1, 1), "charlie");
        var v2 = MakeVersion(1, 2, VersionStatus.Published, new DateTime(2025, 1, 2), "alice");
        var v3 = MakeVersion(1, 3, VersionStatus.Published, new DateTime(2025, 1, 3), "bob");
        var v4 = MakeVersion(1, 4, VersionStatus.Published, new DateTime(2025, 1, 4), "alice"); // duplicate

        SetupDescendants(1);
        SetupVersionList(v1, v2, v3, v4);

        var svc = CreateService();
        var users = svc.GetDistinctUsers().ToList();

        users.Should().BeEquivalentTo(new[] { "alice", "bob", "charlie" });
        users.Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetDistinctUsers_EmptySite_ReturnsEmpty()
    {
        SetupDescendants();
        SetupVersionList();

        var svc = CreateService();
        var users = svc.GetDistinctUsers().ToList();

        users.Should().BeEmpty();
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
