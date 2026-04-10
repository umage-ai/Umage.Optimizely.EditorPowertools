using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Services;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeAudit;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeAudit.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.ContentTypeAudit;

public class ContentTypeAuditServiceTests
{
    private readonly Mock<IContentTypeRepository> _contentTypeRepo;
    private readonly Mock<IContentModelUsage> _contentModelUsage;
    private readonly Mock<IContentLoader> _contentLoader;
    private readonly Mock<IContentSoftLinkRepository> _softLinkRepo;
    private readonly Mock<IPropertyDefinitionRepository> _propertyDefinitionRepo;
    private readonly Mock<ContentTypeStatisticsRepository> _statisticsRepo;
    private readonly ContentTypeAuditService _service;

    public ContentTypeAuditServiceTests()
    {
        EpiServerTestSetup.EnsureInitialized();
        _contentTypeRepo = new Mock<IContentTypeRepository>();
        _contentModelUsage = new Mock<IContentModelUsage>();
        _contentLoader = new Mock<IContentLoader>();
        _softLinkRepo = new Mock<IContentSoftLinkRepository>();
        _propertyDefinitionRepo = new Mock<IPropertyDefinitionRepository>();
        _statisticsRepo = new Mock<ContentTypeStatisticsRepository>();
        var logger = new Mock<ILogger<ContentTypeAuditService>>();

        _service = new ContentTypeAuditService(
            _contentTypeRepo.Object,
            _contentModelUsage.Object,
            _contentLoader.Object,
            _softLinkRepo.Object,
            _propertyDefinitionRepo.Object,
            _statisticsRepo.Object,
            logger.Object);
    }

    private static ContentType CreateContentType(
        int id,
        string name,
        string? displayName = null,
        string? groupName = null,
        Type? modelType = null)
    {
        var ct = new ContentType
        {
            ID = id,
            Name = name,
            DisplayName = displayName,
            GroupName = groupName
        };

        // Set ModelType via reflection since there's no public setter
        if (modelType != null)
        {
            var modelTypeProp = typeof(ContentType).GetProperty("ModelType");
            if (modelTypeProp != null && modelTypeProp.CanWrite)
            {
                modelTypeProp.SetValue(ct, modelType);
            }
        }

        return ct;
    }

    // --- GetAllContentTypes ---

    [Fact]
    public void GetAllContentTypes_ReturnsAllTypesWithStatistics()
    {
        var contentTypes = new[]
        {
            CreateContentType(1, "StandardPage", "Standard Page", "Pages"),
            CreateContentType(2, "ArticlePage", "Article Page", "Pages")
        };
        _contentTypeRepo.Setup(r => r.List()).Returns(contentTypes);

        var stats = new[]
        {
            new ContentTypeStatisticsRecord
            {
                ContentTypeId = 1,
                ContentCount = 50,
                PublishedCount = 40,
                ReferencedCount = 30,
                UnreferencedCount = 20,
                LastUpdated = new DateTime(2025, 1, 1)
            }
        };
        _statisticsRepo.Setup(r => r.GetAll()).Returns(stats);

        var result = _service.GetAllContentTypes().ToList();

        result.Should().HaveCount(2);

        // Type with statistics
        result[0].Id.Should().Be(1);
        result[0].Name.Should().Be("StandardPage");
        result[0].DisplayName.Should().Be("Standard Page");
        result[0].GroupName.Should().Be("Pages");
        result[0].ContentCount.Should().Be(50);
        result[0].PublishedCount.Should().Be(40);
        result[0].ReferencedCount.Should().Be(30);
        result[0].UnreferencedCount.Should().Be(20);
        result[0].StatisticsUpdated.Should().Be(new DateTime(2025, 1, 1));

        // Type without statistics
        result[1].Id.Should().Be(2);
        result[1].Name.Should().Be("ArticlePage");
        result[1].ContentCount.Should().BeNull();
    }

    [Fact]
    public void GetAllContentTypes_EmptyRepository_ReturnsEmpty()
    {
        _contentTypeRepo.Setup(r => r.List()).Returns(Array.Empty<ContentType>());
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetAllContentTypes().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllContentTypes_NoStatisticsAvailable_ReturnsNullCounts()
    {
        var contentTypes = new[]
        {
            CreateContentType(1, "TestPage")
        };
        _contentTypeRepo.Setup(r => r.List()).Returns(contentTypes);
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetAllContentTypes().ToList();

        result[0].ContentCount.Should().BeNull();
        result[0].PublishedCount.Should().BeNull();
        result[0].StatisticsUpdated.Should().BeNull();
    }

    [Fact]
    public void GetAllContentTypes_MapsPropertyCount()
    {
        var ct = CreateContentType(1, "TestPage");
        // PropertyDefinitions is initialized as empty by default
        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { ct });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetAllContentTypes().ToList();

        result[0].PropertyCount.Should().Be(ct.PropertyDefinitions.Count);
    }

    [Fact]
    public void GetAllContentTypes_DetectsOrphanedTypes()
    {
        // ContentType without ModelType is orphaned
        var ct = CreateContentType(1, "OrphanedPage");
        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { ct });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetAllContentTypes().ToList();

        result[0].IsOrphaned.Should().BeTrue();
    }

    [Fact]
    public void GetAllContentTypes_DetectsSystemTypes_ByName()
    {
        var ct = CreateContentType(1, "SysRoot");
        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { ct });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetAllContentTypes().ToList();

        result[0].IsSystemType.Should().BeTrue();
    }

    // --- GetProperties ---

    [Fact]
    public void GetProperties_NonExistentType_ReturnsEmpty()
    {
        _contentTypeRepo.Setup(r => r.Load(It.IsAny<int>())).Returns((ContentType?)null);

        var result = _service.GetProperties(999).ToList();

        result.Should().BeEmpty();
    }

    // --- GetInheritanceTree ---

    [Fact]
    public void GetInheritanceTree_BuildsCorrectHierarchy()
    {
        // Create a simple hierarchy: GrandParent -> Parent -> Child
        // Using real types to test the tree building logic
        var grandParent = CreateContentType(1, "GrandParent", modelType: typeof(GrandParentPage));
        var parent = CreateContentType(2, "Parent", modelType: typeof(ParentPage));
        var child = CreateContentType(3, "Child", modelType: typeof(ChildPage));

        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { grandParent, parent, child });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetInheritanceTree().ToList();

        // GrandParent should be the root (its base PageData is not in the content type list)
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("GrandParent");
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Name.Should().Be("Parent");
        result[0].Children[0].Children.Should().HaveCount(1);
        result[0].Children[0].Children[0].Name.Should().Be("Child");
        result[0].Children[0].Children[0].Children.Should().BeEmpty();
    }

    [Fact]
    public void GetInheritanceTree_MultipleRoots_ReturnsAllRoots()
    {
        var pageType = CreateContentType(1, "APage", modelType: typeof(GrandParentPage));
        var blockType = CreateContentType(2, "BBlock", modelType: typeof(TestBlock));

        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { pageType, blockType });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetInheritanceTree().ToList();

        // Both should be roots since their base types are not in the content type list
        result.Should().HaveCount(2);
        result.Select(r => r.Name).Should().BeEquivalentTo(new[] { "APage", "BBlock" });
    }

    [Fact]
    public void GetInheritanceTree_IncludesStatistics()
    {
        var ct = CreateContentType(1, "TestPage", modelType: typeof(GrandParentPage));
        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { ct });

        var stats = new[]
        {
            new ContentTypeStatisticsRecord { ContentTypeId = 1, ContentCount = 42 }
        };
        _statisticsRepo.Setup(r => r.GetAll()).Returns(stats);

        var result = _service.GetInheritanceTree().ToList();

        result[0].ContentCount.Should().Be(42);
    }

    [Fact]
    public void GetInheritanceTree_EmptyRepository_ReturnsEmpty()
    {
        _contentTypeRepo.Setup(r => r.List()).Returns(Array.Empty<ContentType>());
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetInheritanceTree().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetInheritanceTree_TypesWithoutModelType_ExcludedFromTree()
    {
        // Types with no ModelType are excluded from the inheritance tree
        var orphaned = CreateContentType(1, "OrphanedType");
        var withModel = CreateContentType(2, "WithModel", modelType: typeof(GrandParentPage));

        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { orphaned, withModel });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetInheritanceTree().ToList();

        // Only the type with a model should appear
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("WithModel");
    }

    [Fact]
    public void GetInheritanceTree_SortsByName()
    {
        var typeC = CreateContentType(1, "Charlie", modelType: typeof(SortTestC));
        var typeA = CreateContentType(2, "Alpha", modelType: typeof(SortTestA));
        var typeB = CreateContentType(3, "Bravo", modelType: typeof(SortTestB));

        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { typeC, typeA, typeB });
        _statisticsRepo.Setup(r => r.GetAll()).Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var result = _service.GetInheritanceTree().ToList();

        result.Select(r => r.Name).Should().BeInAscendingOrder();
    }

    // --- Helper types for inheritance tree testing ---

    // Hierarchy: PageData -> GrandParentPage -> ParentPage -> ChildPage
    private class GrandParentPage : EPiServer.Core.PageData { }
    private class ParentPage : GrandParentPage { }
    private class ChildPage : ParentPage { }

    // Independent block type
    private class TestBlock : EPiServer.Core.BlockData { }

    // For sort testing - independent roots
    private class SortTestA : EPiServer.Core.PageData { }
    private class SortTestB : EPiServer.Core.PageData { }
    private class SortTestC : EPiServer.Core.PageData { }
}
