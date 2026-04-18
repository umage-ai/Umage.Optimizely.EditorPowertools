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
#endif
}
