using EPiServer.Personalization.VisitorGroups;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.AudienceManager;
using UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.AudienceManager;

public class AudienceManagerServiceTests
{
    private readonly Mock<IVisitorGroupRepository> _visitorGroupRepo;
    private readonly AudienceManagerService _service;

    public AudienceManagerServiceTests()
    {
        EpiServerTestSetup.EnsureInitialized();
        _visitorGroupRepo = new Mock<IVisitorGroupRepository>();
        var logger = new Mock<ILogger<AudienceManagerService>>();
        // Real repository: tests don't assert on usage counts, and the service
        // swallows DDS-not-available exceptions, so this is safe without a DDS fixture.
        var usageRepo = new PersonalizationUsageRepository();
        _service = new AudienceManagerService(_visitorGroupRepo.Object, usageRepo, logger.Object);
    }

    private static VisitorGroup CreateVisitorGroup(
        string name,
        Guid? id = null,
        string? notes = null,
        params string[] criteriaTypeNames)
    {
        var group = new VisitorGroup
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Notes = notes
        };

        var criteria = new List<VisitorGroupCriterion>();
        foreach (var typeName in criteriaTypeNames)
        {
            criteria.Add(new VisitorGroupCriterion { TypeName = typeName });
        }
        group.Criteria = criteria;

        return group;
    }

    // --- GetAllVisitorGroups ---

    [Fact]
    public void GetAllVisitorGroups_MapsVisitorGroupsToDtos()
    {
        var id = Guid.NewGuid();
        var groups = new[]
        {
            CreateVisitorGroup("Returning Visitors", id, "Some notes",
                "EPiServer.Personalization.VisitorGroups.Criteria.PageVisitedCriterion")
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetAllVisitorGroups().ToList();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(id);
        result[0].Name.Should().Be("Returning Visitors");
        result[0].CleanName.Should().Be("Returning Visitors");
        result[0].Notes.Should().Be("Some notes");
        result[0].CriteriaCount.Should().Be(1);
    }

    [Fact]
    public void GetAllVisitorGroups_WithCategoryPrefix_ExtractsCategory()
    {
        var groups = new[]
        {
            CreateVisitorGroup("[Marketing] Newsletter Subscribers")
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetAllVisitorGroups().ToList();

        result.Should().HaveCount(1);
        result[0].Category.Should().Be("Marketing");
        result[0].CleanName.Should().Be("Newsletter Subscribers");
        result[0].Name.Should().Be("[Marketing] Newsletter Subscribers");
    }

    [Fact]
    public void GetAllVisitorGroups_WithoutCategoryPrefix_CategoryIsNull()
    {
        var groups = new[]
        {
            CreateVisitorGroup("Simple Group Name")
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetAllVisitorGroups().ToList();

        result[0].Category.Should().BeNull();
        result[0].CleanName.Should().Be("Simple Group Name");
    }

    [Fact]
    public void GetAllVisitorGroups_CountsCriteria()
    {
        var groups = new[]
        {
            CreateVisitorGroup("Multi-criteria Group", null, null,
                "Criterion.TypeA",
                "Criterion.TypeB",
                "Criterion.TypeC")
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetAllVisitorGroups().ToList();

        result[0].CriteriaCount.Should().Be(3);
        result[0].Criteria.Should().HaveCount(3);
    }

    [Fact]
    public void GetAllVisitorGroups_EmptyList_ReturnsEmpty()
    {
        _visitorGroupRepo.Setup(r => r.List()).Returns(Array.Empty<VisitorGroup>());

        var result = _service.GetAllVisitorGroups().ToList();

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllVisitorGroups_CleansCriterionTypeNames()
    {
        var groups = new[]
        {
            CreateVisitorGroup("Test Group", null, null,
                "EPiServer.Personalization.VisitorGroups.Criteria.PageVisitedCriterion")
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetAllVisitorGroups().ToList();

        result[0].Criteria[0].TypeName.Should().Be("Page Visited");
    }

    // --- GetCriteria ---

    [Fact]
    public void GetCriteria_ReturnsCriteriaForGroup()
    {
        var id = Guid.NewGuid();
        var groups = new[]
        {
            CreateVisitorGroup("Group", id, null,
                "EPiServer.Personalization.VisitorGroups.Criteria.PageVisitedCriterion",
                "Some.Namespace.UserProfileCriterion")
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetCriteria(id).ToList();

        result.Should().HaveCount(2);
        result[0].TypeName.Should().Be("Page Visited");
        result[1].TypeName.Should().Be("User Profile");
    }

    [Fact]
    public void GetCriteria_NonExistentGroup_ReturnsEmpty()
    {
        _visitorGroupRepo.Setup(r => r.List()).Returns(Array.Empty<VisitorGroup>());

        var result = _service.GetCriteria(Guid.NewGuid()).ToList();

        result.Should().BeEmpty();
    }

    // --- Category pattern edge cases ---

    [Theory]
    [InlineData("[Sales] Hot Leads", "Sales", "Hot Leads")]
    [InlineData("[Tech Support] FAQ Readers", "Tech Support", "FAQ Readers")]
    [InlineData("No Category Here", null, "No Category Here")]
    [InlineData("[Empty]", null, "[Empty]")]  // No space + name after bracket
    public void GetAllVisitorGroups_CategoryExtraction_VariousPatterns(
        string groupName, string? expectedCategory, string expectedCleanName)
    {
        var groups = new[] { CreateVisitorGroup(groupName) };
        _visitorGroupRepo.Setup(r => r.List()).Returns(groups);

        var result = _service.GetAllVisitorGroups().ToList();

        result[0].Category.Should().Be(expectedCategory);
        result[0].CleanName.Should().Be(expectedCleanName);
    }

    [Fact]
    public void GetAllVisitorGroups_NullCriteria_HandledGracefully()
    {
        var group = new VisitorGroup
        {
            Id = Guid.NewGuid(),
            Name = "No Criteria Group",
            Criteria = null!
        };
        _visitorGroupRepo.Setup(r => r.List()).Returns(new[] { group });

        var result = _service.GetAllVisitorGroups().ToList();

        result[0].CriteriaCount.Should().Be(0);
        result[0].Criteria.Should().BeEmpty();
    }
}
