using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Cms13;

internal sealed class Cms13ContentTypeMetadataProvider : IContentTypeMetadataProvider
{
    private readonly IContentTypeRepository _contentTypeRepository;

    public Cms13ContentTypeMetadataProvider(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public ContentTypeMetadata Get(ContentType contentType)
    {
        var contracts = (contentType.Contracts ?? Enumerable.Empty<ContentTypeReference>())
            .Select(ResolveContract)
            .Where(c => c is not null)
            .Cast<ContractRef>()
            .ToList();

        var behaviors = (contentType.CompositionBehaviors ?? Enumerable.Empty<CompositionBehavior>())
            .Select(cb => cb.ToString())
            .ToList();

        return new ContentTypeMetadata(
            IsContract: contentType.IsContract,
            Contracts: contracts,
            CompositionBehaviors: behaviors);
    }

    private ContractRef? ResolveContract(ContentTypeReference reference)
    {
        // Load by GUID rather than ID: ContentTypeReference only exposes GUID,
        // and GUIDs are environment-stable across deployments while IDs are per-database.
        var ct = _contentTypeRepository.Load(reference.GUID);
        if (ct == null) return null;
        return new ContractRef(ct.ID, ct.GUID, ct.Name, ct.DisplayName);
    }
}
