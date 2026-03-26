using EPiServer.Core;

namespace EditorPowertools.Helpers;

/// <summary>
/// A property found during content traversal, with its context (property path, owning content).
/// </summary>
public record DiscoveredProperty(
    PropertyData Property,
    string PropertyPath,
    IContentData OwningContent,
    int Depth);

/// <summary>
/// Generic recursive helper that traverses all properties of a content item,
/// including nested blocks (inline blocks, block properties, lists of blocks).
///
/// Use this when scanning content for links, personalization, or any other analysis
/// that needs to look inside all property types recursively.
/// </summary>
public static class ContentPropertyTraverser
{
    /// <summary>
    /// Recursively yields all properties from a content item, including:
    /// - Direct properties (string, XhtmlString, Url, ContentReference, ContentArea, etc.)
    /// - Properties inside block properties (IContentData values)
    /// - Properties inside ContentArea items (loaded as IContent)
    /// - Properties inside list/collection properties (IList&lt;T&gt; where T : IContentData)
    /// </summary>
    /// <param name="content">The content item to traverse.</param>
    /// <param name="contentLoader">Content loader for resolving ContentArea items.</param>
    /// <param name="maxDepth">Maximum recursion depth (default 5) to prevent infinite loops.</param>
    /// <param name="propertyPrefix">Internal: prefix for building dotted property paths.</param>
    /// <param name="depth">Internal: current recursion depth.</param>
    public static IEnumerable<DiscoveredProperty> TraverseProperties(
        IContentData content,
        EPiServer.IContentLoader? contentLoader = null,
        int maxDepth = 5,
        string propertyPrefix = "",
        int depth = 0)
    {
        if (depth > maxDepth) yield break;

        foreach (var prop in content.Property)
        {
            if (prop.Value == null) continue;
            if (IsSystemProperty(prop.Name)) continue;

            var propertyPath = string.IsNullOrEmpty(propertyPrefix)
                ? prop.Name
                : $"{propertyPrefix}.{prop.Name}";

            // Yield the property itself
            yield return new DiscoveredProperty(prop, propertyPath, content, depth);

            // Recurse into nested content types

            // 1. Block property (property value is IContentData but not ContentArea/XhtmlString)
            if (prop.Value is IContentData nestedContent
                && prop.Value is not ContentArea
                && prop.Value is not XhtmlString)
            {
                foreach (var nested in TraverseProperties(nestedContent, contentLoader, maxDepth, propertyPath, depth + 1))
                    yield return nested;
            }

            // 2. ContentArea - traverse each item's content
            if (prop.Value is ContentArea contentArea && contentLoader != null)
            {
                var caItems = GetContentAreaItems(contentArea, contentLoader);
                foreach (var (itemContent, itemRef) in caItems)
                {
                    foreach (var nested in TraverseProperties(itemContent, contentLoader, maxDepth,
                        $"{propertyPath}[{itemRef.ID}]", depth + 1))
                        yield return nested;
                }
            }

            // 3. Lists/collections of IContentData (e.g. IList<BlockData>, IList<XhtmlString>)
            if (prop.Value is System.Collections.IEnumerable enumerable
                && prop.Value is not string
                && prop.Value is not ContentArea
                && prop.Value is not XhtmlString)
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    if (item is IContentData listItem)
                    {
                        foreach (var nested in TraverseProperties(listItem, contentLoader, maxDepth,
                            $"{propertyPath}[{index}]", depth + 1))
                            yield return nested;
                    }
                    else if (item is XhtmlString || item is EPiServer.Url || item is ContentReference)
                    {
                        // Yield list items that are interesting types
                        // Create a synthetic PropertyData-like wrapper isn't practical,
                        // so we just note these exist at this path
                    }
                    index++;
                }
            }
        }
    }

    private static List<(IContent content, ContentReference contentRef)> GetContentAreaItems(
        ContentArea contentArea, EPiServer.IContentLoader contentLoader)
    {
        var results = new List<(IContent, ContentReference)>();
        foreach (var item in contentArea.Items)
        {
            if (item.ContentLink == null || ContentReference.IsNullOrEmpty(item.ContentLink))
                continue;
            try
            {
                if (contentLoader.TryGet<IContent>(item.ContentLink, out var itemContent))
                    results.Add((itemContent, item.ContentLink));
            }
            catch { /* Skip inaccessible */ }
        }
        return results;
    }

    private static bool IsSystemProperty(string name)
    {
        // Skip EPiServer internal system properties
        return name switch
        {
            "PageLink" or "PageTypeID" or "PageParentLink" or "PagePendingPublish"
                or "PageWorkStatus" or "PageDeleted" or "PageSaved" or "PageTypeName"
                or "PageChanged" or "PageCreated" or "PageMasterLanguageBranch"
                or "PageLanguageBranch" or "PageGUID" or "PageContentAssetsID"
                or "PageContentOwnerID" or "PageFolderID" or "PageShortcutType"
                or "PageShortcutLink" or "PageTargetFrame" or "PageExternalURL"
                or "PageStartPublish" or "PageStopPublish" or "PageCreatedBy"
                or "PageChangedBy" or "PageChangedOnPublish" or "PageCategory" => true,
            _ => false
        };
    }
}
