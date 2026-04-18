using EPiServer.DataAbstraction;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
#if OPTIMIZELY_CMS12
using UmageAI.Optimizely.EditorPowerTools.Cms12;
#endif
#if OPTIMIZELY_CMS13
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Cms13;
#endif

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools;

public class ContentTypeMetadataProviderTests
{
    public ContentTypeMetadataProviderTests()
    {
        EpiServerTestSetup.EnsureInitialized();
    }

#if OPTIMIZELY_CMS12
    [Fact]
    public void Cms12Provider_AlwaysReturnsEmpty()
    {
        var provider = new Cms12ContentTypeMetadataProvider();
        var ct = new ContentType { ID = 1, Name = "Test" };

        var result = provider.Get(ct);

        result.Should().Be(ContentTypeMetadata.Empty);
        result.IsContract.Should().BeFalse();
        result.Contracts.Should().BeEmpty();
        result.CompositionBehaviors.Should().BeEmpty();
    }
#endif

#if OPTIMIZELY_CMS13
    [Fact]
    public void Cms13Provider_ReadsRealMembers()
    {
        var repo = new Mock<IContentTypeRepository>();
        var provider = new Cms13ContentTypeMetadataProvider(repo.Object);

        var ct = new ContentType { ID = 1, Name = "Test" };
        typeof(ContentType).GetProperty("CompositionBehaviors")!
            .SetValue(ct, new[] { CompositionBehavior.SectionEnabled });
        typeof(ContentType).GetProperty("IsContract")!
            .SetValue(ct, false);
        typeof(ContentType).GetProperty("Contracts")!
            .SetValue(ct, Array.Empty<ContentTypeReference>());

        var result = provider.Get(ct);

        result.IsContract.Should().BeFalse();
        result.CompositionBehaviors.Should().ContainSingle().Which.Should().Be("SectionEnabled");
        result.Contracts.Should().BeEmpty();
    }

    [Fact]
    public void Cms13Provider_ProjectsContractsAndFiltersMissingReferences()
    {
        var resolvedGuid = Guid.NewGuid();
        var danglingGuid = Guid.NewGuid();

        var resolvedContract = new ContentType
        {
            ID = 42,
            GUID = resolvedGuid,
            Name = "IMyContract",
            DisplayName = "My Contract",
        };

        var repo = new Mock<IContentTypeRepository>();
        repo.Setup(r => r.Load(resolvedGuid)).Returns(resolvedContract);
        repo.Setup(r => r.Load(danglingGuid)).Returns((ContentType?)null);

        var provider = new Cms13ContentTypeMetadataProvider(repo.Object);

        var resolvedRef = new ContentTypeReference();
        typeof(ContentTypeReference).GetProperty("GUID")!.SetValue(resolvedRef, resolvedGuid);
        var danglingRef = new ContentTypeReference();
        typeof(ContentTypeReference).GetProperty("GUID")!.SetValue(danglingRef, danglingGuid);

        var ct = new ContentType { ID = 1, Name = "Test" };
        typeof(ContentType).GetProperty("CompositionBehaviors")!
            .SetValue(ct, Array.Empty<CompositionBehavior>());
        typeof(ContentType).GetProperty("IsContract")!
            .SetValue(ct, false);
        typeof(ContentType).GetProperty("Contracts")!
            .SetValue(ct, new[] { resolvedRef, danglingRef });

        var result = provider.Get(ct);

        result.Contracts.Should().ContainSingle();
        var only = result.Contracts[0];
        only.Id.Should().Be(42);
        only.Guid.Should().Be(resolvedGuid);
        only.Name.Should().Be("IMyContract");
        only.DisplayName.Should().Be("My Contract");
    }
#endif
}
