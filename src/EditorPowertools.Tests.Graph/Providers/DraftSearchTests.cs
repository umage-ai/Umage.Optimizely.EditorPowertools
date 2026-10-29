using EPiServer.DataAbstraction;
using EPiServer.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Providers;

public class DraftSearchTests
{
    private static ContentVersion Version(int id) =>
        new(new ContentReference(id, 3), "n", VersionStatus.CheckedOut, DateTime.UtcNow, "u", "u", 1, "en", false, false);

    private static (DraftSearch, Mock<IContentVersionRepository>) Create()
    {
        var repo = new Mock<IContentVersionRepository>();
        return (new DraftSearch(repo.Object, NullLogger<DraftSearch>.Instance), repo);
    }

    private delegate void ListCb(VersionFilter f, int s, int n, out int t);

    [Fact]
    public void BuildsFilter()
    {
        var (sut, repo) = Create();
        VersionFilter? captured = null;
        int total;
        repo.Setup(r => r.List(It.IsAny<VersionFilter>(), 0, 40, out total))
            .Callback(new ListCb((VersionFilter f, int s, int n, out int t) => { captured = f; t = 0; }))
            .Returns(Array.Empty<ContentVersion>());

        sut.FindDrafts("abc", "da", 10, new HashSet<int>());

        captured.Should().NotBeNull();
        captured!.Name.Should().Be("abc");
        captured.ProviderName.Should().Be(string.Empty);
        captured.ExcludeDeleted.Should().BeTrue();
        captured.SortColumn.Should().Be(VersionSortColumn.Saved);
        captured.SortDirection.Should().Be(VersionSortDirection.Descending);
        captured.Statuses.Should().BeEquivalentTo(new[]
        {
            VersionStatus.CheckedOut, VersionStatus.CheckedIn, VersionStatus.Rejected,
            VersionStatus.DelayedPublish, VersionStatus.AwaitingApproval
        });
        captured.Languages.Should().ContainSingle().Which.Name.Should().Be("da");
    }

    [Fact]
    public void DeduplicatesAndSkipsAlreadyFound()
    {
        var (sut, repo) = Create();
        int total;
        repo.Setup(r => r.List(It.IsAny<VersionFilter>(), 0, 40, out total))
            .Returns(new[] { Version(1), Version(1), Version(2), Version(3) });

        var result = sut.FindDrafts("x", null, 10, new HashSet<int> { 2 });

        result.Select(r => r.ID).Should().Equal(1, 3);
        result.Should().OnlyContain(r => r.WorkID == 0);
    }

    [Fact]
    public void InvalidCulture_ReturnsEmpty()
    {
        var (sut, _) = Create();
        sut.FindDrafts("x", "xx-invalid-!!", 10, new HashSet<int>()).Should().BeEmpty();
    }

    [Fact]
    public void RepositoryThrows_ReturnsEmpty()
    {
        var (sut, repo) = Create();
        int total;
        repo.Setup(r => r.List(It.IsAny<VersionFilter>(), It.IsAny<int>(), It.IsAny<int>(), out total))
            .Throws(new InvalidOperationException("boom"));
        sut.FindDrafts("x", null, 10, new HashSet<int>()).Should().BeEmpty();
    }
}
