using UmageAI.Optimizely.EditorPowerTools.Tools.LinkChecker;
using UmageAI.Optimizely.EditorPowerTools.Tools.LinkChecker.Models;
using FluentAssertions;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.LinkChecker;

public class LinkCheckerServiceTests
{
    private readonly Mock<LinkCheckerRepository> _repositoryMock;
    private readonly LinkCheckerService _sut;

    public LinkCheckerServiceTests()
    {
        _repositoryMock = new Mock<LinkCheckerRepository>();
        _sut = new LinkCheckerService(_repositoryMock.Object);
    }

    private static LinkCheckRecord CreateRecord(
        int contentId = 1,
        string url = "https://example.com",
        string linkType = "External",
        int statusCode = 200,
        bool isValid = true)
    {
        return new LinkCheckRecord
        {
            ContentId = contentId,
            ContentName = $"Page {contentId}",
            ContentTypeName = "StandardPage",
            PropertyName = "MainBody",
            Url = url,
            LinkType = linkType,
            StatusCode = statusCode,
            StatusText = isValid ? "OK" : "Not Found",
            IsValid = isValid,
            LastChecked = DateTime.UtcNow
        };
    }

    [Fact]
    public void GetAllLinks_ReturnsAllStoredLinks()
    {
        var records = new List<LinkCheckRecord>
        {
            CreateRecord(1, "https://a.com"),
            CreateRecord(2, "https://b.com"),
            CreateRecord(3, "https://c.com")
        };
        _repositoryMock.Setup(r => r.GetAll()).Returns(records);

        var result = _sut.GetAllLinks().ToList();

        result.Should().HaveCount(3);
        result.Select(l => l.Url).Should().BeEquivalentTo("https://a.com", "https://b.com", "https://c.com");
    }

    [Fact]
    public void GetAllLinks_MapsAllPropertiesCorrectly()
    {
        var record = CreateRecord(42, "https://example.com/page");
        record.FriendlyUrl = "/page";
        record.TargetContentId = 99;
        record.Breadcrumb = "Home > Page";
        record.EditUrl = "/edit/42";
        record.UsedOn = "Start Page";
        record.UsedOnEditUrls = "Start|/|/edit/1";
        _repositoryMock.Setup(r => r.GetAll()).Returns(new[] { record });

        var dto = _sut.GetAllLinks().Single();

        dto.ContentId.Should().Be(42);
        dto.ContentName.Should().Be("Page 42");
        dto.ContentTypeName.Should().Be("StandardPage");
        dto.PropertyName.Should().Be("MainBody");
        dto.Url.Should().Be("https://example.com/page");
        dto.FriendlyUrl.Should().Be("/page");
        dto.TargetContentId.Should().Be(99);
        dto.LinkType.Should().Be("External");
        dto.StatusCode.Should().Be(200);
        dto.StatusText.Should().Be("OK");
        dto.IsValid.Should().BeTrue();
        dto.Breadcrumb.Should().Be("Home > Page");
        dto.EditUrl.Should().Be("/edit/42");
        dto.UsedOn.Should().Be("Start Page");
        dto.UsedOnEditUrls.Should().Be("Start|/|/edit/1");
    }

    [Fact]
    public void GetBrokenLinks_FiltersToInvalidLinks()
    {
        var brokenRecords = new List<LinkCheckRecord>
        {
            CreateRecord(1, "https://broken.com", statusCode: 404, isValid: false),
            CreateRecord(2, "https://timeout.com", statusCode: 0, isValid: false)
        };
        _repositoryMock.Setup(r => r.GetByStatus(false)).Returns(brokenRecords);

        var result = _sut.GetBrokenLinks().ToList();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => !l.IsValid);
    }

    [Fact]
    public void GetBrokenLinks_CallsRepositoryWithFalseStatus()
    {
        _repositoryMock.Setup(r => r.GetByStatus(false)).Returns(Enumerable.Empty<LinkCheckRecord>());

        _sut.GetBrokenLinks().ToList();

        _repositoryMock.Verify(r => r.GetByStatus(false), Times.Once);
    }

    [Fact]
    public void GetStats_CalculatesCorrectTotals()
    {
        var records = new List<LinkCheckRecord>
        {
            CreateRecord(1, "https://ok.com", "External", 200, true),
            CreateRecord(2, "https://ok2.com", "Internal", 200, true),
            CreateRecord(3, "https://broken.com", "External", 404, false),
            CreateRecord(4, "/internal-broken", "Internal", 500, false),
            CreateRecord(5, "https://fine.com", "External", 200, true)
        };
        _repositoryMock.Setup(r => r.GetAll()).Returns(records);

        var stats = _sut.GetStats();

        stats.TotalLinks.Should().Be(5);
        stats.BrokenLinks.Should().Be(2);
        stats.ValidLinks.Should().Be(3);
        stats.InternalLinks.Should().Be(2);
        stats.ExternalLinks.Should().Be(3);
    }

    [Fact]
    public void GetStats_AllBroken_ReturnsCorrectCounts()
    {
        var records = new List<LinkCheckRecord>
        {
            CreateRecord(1, "https://a.com", "External", 404, false),
            CreateRecord(2, "https://b.com", "External", 500, false)
        };
        _repositoryMock.Setup(r => r.GetAll()).Returns(records);

        var stats = _sut.GetStats();

        stats.TotalLinks.Should().Be(2);
        stats.BrokenLinks.Should().Be(2);
        stats.ValidLinks.Should().Be(0);
    }

    [Fact]
    public void GetAllLinks_EmptyRepository_ReturnsEmptyCollection()
    {
        _repositoryMock.Setup(r => r.GetAll()).Returns(Enumerable.Empty<LinkCheckRecord>());

        var result = _sut.GetAllLinks().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetBrokenLinks_EmptyRepository_ReturnsEmptyCollection()
    {
        _repositoryMock.Setup(r => r.GetByStatus(false)).Returns(Enumerable.Empty<LinkCheckRecord>());

        var result = _sut.GetBrokenLinks().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetStats_EmptyRepository_ReturnsAllZeros()
    {
        _repositoryMock.Setup(r => r.GetAll()).Returns(Enumerable.Empty<LinkCheckRecord>());

        var stats = _sut.GetStats();

        stats.TotalLinks.Should().Be(0);
        stats.BrokenLinks.Should().Be(0);
        stats.ValidLinks.Should().Be(0);
        stats.InternalLinks.Should().Be(0);
        stats.ExternalLinks.Should().Be(0);
    }
}
