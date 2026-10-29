using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

/// <summary>
/// Helpers for the shell's "search roots" (the tree branches an editor narrowed the search to).
/// </summary>
internal static class SearchRoots
{
    /// <summary>Parses shell search roots into version-less references; ignores unparseable/empty.</summary>
    public static IReadOnlySet<ContentReference> Parse(IEnumerable<string>? roots)
    {
        var result = new HashSet<ContentReference>();
        if (roots is null)
        {
            return result;
        }

        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            if (ContentReference.TryParse(root, out var parsed) && !ContentReference.IsNullOrEmpty(parsed))
            {
                result.Add(parsed.ToReferenceWithoutVersion());
            }
        }

        return result;
    }

    /// <summary>True when no roots, or content is a root, or any ancestor is a root.</summary>
    /// <remarks>
    /// Compares parsed references rather than strings: roots may arrive in provider form
    /// (<c>-1073741823__CatalogContent</c>) while a bare ContentLink.ID is just the integer.
    /// </remarks>
    public static bool Contains(IContent content, IReadOnlySet<ContentReference> roots, IContentLoader loader)
    {
        if (roots.Count == 0)
        {
            return true;
        }

        if (roots.Contains(content.ContentLink.ToReferenceWithoutVersion()))
        {
            return true;
        }

        return loader.GetAncestors(content.ContentLink)
            .Any(a => roots.Contains(a.ContentLink.ToReferenceWithoutVersion()));
    }

    /// <summary>GUIDs of loadable roots (for the Graph Ancestors constraint).</summary>
    public static IReadOnlyList<Guid> ToGuids(IReadOnlySet<ContentReference> roots, IContentLoader loader)
    {
        var guids = new List<Guid>();
        foreach (var root in roots)
        {
            if (loader.TryGet<IContent>(root, out var content) && content is not null
                && content.ContentGuid != Guid.Empty)
            {
                guids.Add(content.ContentGuid);
            }
        }

        return guids;
    }
}
