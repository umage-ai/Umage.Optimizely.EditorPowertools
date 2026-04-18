using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

/// <summary>
/// Exposes CMS-version-specific ContentType metadata through a stable shape.
/// Under CMS 12 all values are empty/false. Under CMS 13 values reflect real data.
/// Consumers never branch on CMS version themselves.
/// </summary>
public interface IContentTypeMetadataProvider
{
    ContentTypeMetadata Get(ContentType contentType);
}
