using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Cms12;

/// <summary>CMS 12 has no Contracts or CompositionBehaviors; always returns Empty.</summary>
internal sealed class Cms12ContentTypeMetadataProvider : IContentTypeMetadataProvider
{
    public ContentTypeMetadata Get(ContentType contentType) => ContentTypeMetadata.Empty;
}
