using System.Globalization;
using System.Web;
using EPiServer.Cms.Shell;
using EPiServer.Core;
using EPiServer.Editor;
using EPiServer.Framework.Localization;
using EPiServer.Shell;
using EPiServer.Shell.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

/// <summary>
/// Builds CMS UI <see cref="SearchResult"/>s for content, reproducing the metadata the built-in
/// providers emit (the assets pane and result list read these keys, PascalCase, silently).
/// </summary>
public class SearchResultFactory
{
    private const string DraftKey = "/editorpowertools/graph/search/unpublisheddraft";

    private readonly IContentTypeRepository _types;
    private readonly UIDescriptorRegistry _descriptors;
    private readonly LocalizationService _loc;

    public SearchResultFactory(IContentTypeRepository types, UIDescriptorRegistry descriptors, LocalizationService loc)
    {
        _types = types;
        _descriptors = descriptors;
        _loc = loc;
    }

    public virtual SearchResult Create(IContent content, bool isDraft, string? requestedLanguage, string iconCssClass)
    {
        var localizable = content as ILocalizable;
        var branch = localizable?.Language?.Name ?? string.Empty;

        var parts = new List<string>();
        var typeName = ResolveTypeName(content);
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            parts.Add(typeName);
        }

        if (isDraft)
        {
            parts.Add(_loc.GetStringByCulture(DraftKey, "Unpublished draft", CultureInfo.CurrentUICulture));
        }

        var detail = string.Join(" · ", parts);
        // The title is written to innerHTML by the CMS result list, so it must be encoded.
        var title = BuildTitle(content.Name, detail);

        var result = new SearchResult(AppendLanguage(ResolveEditUrl(content), branch), title, detail)
        {
            IconCssClass = iconCssClass,
            Language = localizable?.Language?.NativeName ?? branch
        };

        var untranslated = !string.IsNullOrEmpty(requestedLanguage)
            && !string.IsNullOrEmpty(branch)
            && !branch.Equals(requestedLanguage, StringComparison.OrdinalIgnoreCase);

        // PascalCase: ContentSearchQuery keeps only results with Metadata["Id"] (a full ContentLink string).
        result.Metadata["Id"] = content.ContentLink.ToString();
        result.Metadata["ParentId"] = content.ParentLink?.ToString() ?? string.Empty;
        result.Metadata["IsOnCurrentHost"] = "true";
        result.Metadata["TypeId"] = content.ContentTypeID.ToString();
        result.Metadata["TypeIdentifier"] = ResolveTypeIdentifier(content) ?? string.Empty;
        result.Metadata["ThumbnailUrl"] = string.Empty;
        result.Metadata["LanguageBranch"] = branch;
        result.Metadata["IsUntranslatedContent"] = untranslated ? "true" : "false";
        return result;
    }

    protected virtual string? ResolveTypeIdentifier(IContent content) => content.GetTypeIdentifier(_descriptors);

    protected virtual string ResolveEditUrl(IContent content) => PageEditing.GetEditUrl(content.ContentLink);

    private string? ResolveTypeName(IContent content)
    {
        try
        {
            var type = _types.Load(content.ContentTypeID);
            if (type is null)
            {
                return null;
            }

            return new[] { type.LocalizedName, type.DisplayName, type.Name }
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>HTML-encoded "name  —  detail".</summary>
    internal static string BuildTitle(string? name, string? detail) =>
        HttpUtility.HtmlEncode(
            string.IsNullOrWhiteSpace(detail) ? name ?? string.Empty : $"{name}  —  {detail}");

    /// <summary>Inserts ?language= before '#' (the shell cannot resolve context without it).</summary>
    internal static string AppendLanguage(string editUrl, string? branch)
    {
        if (string.IsNullOrEmpty(branch))
        {
            return editUrl;
        }

        var fragment = editUrl.IndexOf('#');
        return fragment < 0
            ? $"{editUrl}?language={branch}"
            : $"{editUrl[..fragment]}?language={branch}{editUrl[fragment..]}";
    }
}
