using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Security;
using EPiServer.Web.Routing;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ContentAudit;

/// <summary>
/// Unit tests for GetDescendentsContentAuditProvider's per-item resilience (issue #63).
///
/// Real-world CMS databases contain occasional content items whose property data can no
/// longer be materialized (e.g. EPiServer's ListPropertyValueConverter throwing NRE for
/// orphaned list-property rows). One such item must not abort a whole-tree export: the
/// provider should skip it, log a warning, and keep enumerating.
/// </summary>
public class GetDescendentsContentAuditProviderTests
{
    private readonly Mock<IContentRepository> _contentRepo = new();
    private readonly Mock<IContentTypeRepository> _contentTypeRepo = new();
    private readonly Mock<IContentVersionRepository> _versionRepo = new();
    private readonly Mock<ILanguageBranchRepository> _languageBranchRepo = new();
    private readonly Mock<IUrlResolver> _urlResolver = new();
    private readonly Mock<IContentAccessEvaluator> _accessEvaluator = new();

    private GetDescendentsContentAuditProvider CreateProvider()
    {
        _accessEvaluator
            .Setup(a => a.HasAccess(It.IsAny<IContent>(), It.IsAny<System.Security.Principal.IPrincipal>(), It.IsAny<AccessLevel>()))
            .Returns(true);

        return new GetDescendentsContentAuditProvider(
            _contentRepo.Object,
            _contentTypeRepo.Object,
            _versionRepo.Object,
            _languageBranchRepo.Object,
            _urlResolver.Object,
            _accessEvaluator.Object,
            NullLogger<GetDescendentsContentAuditProvider>.Instance);
    }

    private static Mock<IContent> MakeContent(int id, string name)
    {
        var content = new Mock<IContent>();
        content.SetupGet(c => c.ContentLink).Returns(new ContentReference(id));
        content.SetupGet(c => c.Name).Returns(name);
        content.SetupGet(c => c.ContentTypeID).Returns(1);
        return content;
    }

    /// <summary>Content whose property data blows up on access, like EPiServer's
    /// ListPropertyValueConverter NRE for corrupt list-property rows.</summary>
    private static Mock<IContent> MakePoisonContent(int id)
    {
        var content = new Mock<IContent>();
        content.SetupGet(c => c.ContentLink).Returns(new ContentReference(id));
        content.SetupGet(c => c.Name).Throws(new NullReferenceException(
            "Object reference not set to an instance of an object. (ListPropertyValueConverter.SetValue)"));
        content.SetupGet(c => c.ContentTypeID).Returns(1);
        return content;
    }

    private void SetupTree(params Mock<IContent>[] contents)
    {
        var refs = contents.Select(c => c.Object.ContentLink).ToList();
        _contentRepo.Setup(r => r.GetDescendents(ContentReference.RootPage)).Returns(refs);
        foreach (var c in contents)
        {
            var link = c.Object.ContentLink;
            _contentRepo.Setup(r => r.Get<IContent>(link)).Returns(c.Object);
        }
    }

    [Fact]
    public void GetAllRows_SkipsItemWhosePropertyAccessThrows_AndReturnsRemainingRows()
    {
        SetupTree(MakeContent(1, "Good A"), MakePoisonContent(2), MakeContent(3, "Good B"));
        var provider = CreateProvider();
        var request = new ContentAuditExportRequest { Format = "csv", Columns = ["name"] };

        List<ContentAuditRow> rows = [];
        var act = () => rows = provider.GetAllRows(request).ToList();

        act.Should().NotThrow("one corrupt content item must not abort the whole export");
        rows.Select(r => r.Name).Should().Equal("Good A", "Good B");
    }

    [Fact]
    public void GetPage_SkipsItemWhosePropertyAccessThrows_AndReturnsRemainingRows()
    {
        SetupTree(MakeContent(1, "Good A"), MakePoisonContent(2), MakeContent(3, "Good B"));
        var provider = CreateProvider();
        var request = new ContentAuditRequest { Page = 1, PageSize = 10, Columns = ["name"] };

        ContentAuditPageResult result = null!;
        var act = () => result = provider.GetPage(request);

        act.Should().NotThrow("one corrupt content item must not abort the audit page");
        result.Items.Select(r => r.Name).Should().Equal("Good A", "Good B");
    }

    [Fact]
    public void GetAllRows_SkipsItemThatFailsToLoad_AndReturnsRemainingRows()
    {
        var good = MakeContent(1, "Good A");
        var badRef = new ContentReference(2);
        _contentRepo.Setup(r => r.GetDescendents(ContentReference.RootPage))
            .Returns([good.Object.ContentLink, badRef]);
        _contentRepo.Setup(r => r.Get<IContent>(good.Object.ContentLink)).Returns(good.Object);
        _contentRepo.Setup(r => r.Get<IContent>(badRef)).Throws(new NullReferenceException("ContentDB.ReadPropertyData"));

        var provider = CreateProvider();
        var request = new ContentAuditExportRequest { Format = "csv", Columns = ["name"] };

        var rows = provider.GetAllRows(request).ToList();

        rows.Select(r => r.Name).Should().Equal("Good A");
    }
}
