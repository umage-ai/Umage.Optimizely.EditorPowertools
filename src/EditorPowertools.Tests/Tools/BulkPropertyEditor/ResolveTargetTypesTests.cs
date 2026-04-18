using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.BulkPropertyEditor;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.BulkPropertyEditor;

public class ResolveTargetTypesTests
{
    public ResolveTargetTypesTests()
    {
        EpiServerTestSetup.EnsureInitialized();
    }

    [Fact]
    public void IdenticalWhenNoContractsSelected()
    {
        var repo = new Mock<IContentTypeRepository>();
        var meta = new Mock<IContentTypeMetadataProvider>();

        var concrete1 = new ContentType { ID = 10, Name = "Promo" };
        var concrete2 = new ContentType { ID = 11, Name = "Hero" };
        repo.Setup(r => r.List()).Returns(new[] { concrete1, concrete2 });
        meta.Setup(m => m.Get(It.IsAny<ContentType>())).Returns(ContentTypeMetadata.Empty);

        var service = CreateService(repo.Object, meta.Object);

        var result = service.ResolveTargetTypes(new[] { 10, 11 });

        result.Should().BeEquivalentTo(new[] { 10, 11 });
    }

    [Fact]
    public void EmptyRequestReturnsEmpty()
    {
        var repo = new Mock<IContentTypeRepository>();
        var meta = new Mock<IContentTypeMetadataProvider>();

        var service = CreateService(repo.Object, meta.Object);

        var result = service.ResolveTargetTypes(Array.Empty<int>());

        result.Should().BeEmpty();
    }

#if OPTIMIZELY_CMS13
    [Fact]
    public void ContractSelectionExpandsToImplementingTypes()
    {
        var repo = new Mock<IContentTypeRepository>();
        var meta = new Mock<IContentTypeMetadataProvider>();

        var contract = new ContentType { ID = 100, Name = "IHasSeo" };
        var impl1 = new ContentType { ID = 10, Name = "Promo" };
        var impl2 = new ContentType { ID = 11, Name = "Hero" };
        var other = new ContentType { ID = 12, Name = "Footer" };

        repo.Setup(r => r.List()).Returns(new[] { contract, impl1, impl2, other });

        var contractRef = new ContractRef(100, Guid.Empty, "IHasSeo", null);
        meta.Setup(m => m.Get(contract)).Returns(new ContentTypeMetadata(
            IsContract: true, Contracts: Array.Empty<ContractRef>(), CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(impl1)).Returns(new ContentTypeMetadata(
            IsContract: false, Contracts: new[] { contractRef }, CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(impl2)).Returns(new ContentTypeMetadata(
            IsContract: false, Contracts: new[] { contractRef }, CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(other)).Returns(ContentTypeMetadata.Empty);

        var service = CreateService(repo.Object, meta.Object);

        var result = service.ResolveTargetTypes(new[] { 100 });

        result.Should().BeEquivalentTo(new[] { 10, 11 });
    }

    [Fact]
    public void MixedContractAndConcreteSelectionDeduplicates()
    {
        var repo = new Mock<IContentTypeRepository>();
        var meta = new Mock<IContentTypeMetadataProvider>();

        var contract = new ContentType { ID = 100, Name = "IHasSeo" };
        var impl1 = new ContentType { ID = 10, Name = "Promo" };
        var impl2 = new ContentType { ID = 11, Name = "Hero" };

        repo.Setup(r => r.List()).Returns(new[] { contract, impl1, impl2 });

        var contractRef = new ContractRef(100, Guid.Empty, "IHasSeo", null);
        meta.Setup(m => m.Get(contract)).Returns(new ContentTypeMetadata(
            IsContract: true, Contracts: Array.Empty<ContractRef>(), CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(impl1)).Returns(new ContentTypeMetadata(
            IsContract: false, Contracts: new[] { contractRef }, CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(impl2)).Returns(new ContentTypeMetadata(
            IsContract: false, Contracts: new[] { contractRef }, CompositionBehaviors: Array.Empty<string>()));

        var service = CreateService(repo.Object, meta.Object);

        // Select both the contract AND impl1 explicitly; impl1 should appear once (not twice).
        var result = service.ResolveTargetTypes(new[] { 100, 10 });

        result.Should().BeEquivalentTo(new[] { 10, 11 });
    }
#endif

    private static BulkPropertyEditorService CreateService(
        IContentTypeRepository repo, IContentTypeMetadataProvider meta)
    {
        return new BulkPropertyEditorService(
            repo,
            Mock.Of<IContentRepository>(),
            Mock.Of<IContentModelUsage>(),
            Mock.Of<ILanguageBranchRepository>(),
            meta,
            Mock.Of<ILogger<BulkPropertyEditorService>>());
    }
}
