using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

/// <summary>
/// Pluggable backing store for content-name search in the addon's content picker.
///
/// Editor Powertools ships <em>without</em> a default implementation: walking the content
/// tree on every keystroke is forbidden on the live path (see CLAUDE.md / project rules),
/// and we don't bundle a search engine. A separate package — <c>EditorPowertools.Graph</c>
/// or similar — registers an implementation backed by Optimizely Graph / Find / a custom
/// index. When no provider is registered, search returns no results and editors must
/// navigate the tree.
/// </summary>
public interface IContentSearchProvider
{
    /// <summary>
    /// Search for content whose name matches <paramref name="query"/>, optionally scoped to
    /// descendants of <paramref name="root"/>. Implementations should respect the calling
    /// user's read access. The result is capped to <paramref name="maxResults"/>.
    /// </summary>
    IEnumerable<ContentSearchHit> Search(string query, ContentReference? root, int maxResults);
}

/// <summary>
/// Lightweight result row for content search — just enough for the picker UI to render.
/// </summary>
public sealed class ContentSearchHit
{
    public int ContentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public bool HasChildren { get; init; }
}
