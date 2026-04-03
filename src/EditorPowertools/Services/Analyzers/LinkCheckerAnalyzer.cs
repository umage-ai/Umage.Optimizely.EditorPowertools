using System.Net;
using System.Text.RegularExpressions;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Localization;
using EPiServer.Shell;
using EPiServer.Web.Routing;
using EditorPowertools.Helpers;
using EditorPowertools.Tools.LinkChecker;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Services.Analyzers;

/// <summary>
/// Analyzer that extracts links from content properties during Analyze()
/// and checks them (HTTP validation, block usage resolution) in Finalize().
/// </summary>
public class LinkCheckerAnalyzer : IContentAnalyzer
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IUrlResolver _urlResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LinkCheckerRepository _linkCheckerRepository;
    private readonly LocalizationService _localizationService;
    private readonly ILogger<LinkCheckerAnalyzer> _logger;

    private readonly List<LinkEntry> _linkEntries = new();

    public string Name => _localizationService.GetString("/editorpowertools/analyzers/linkchecker");

    public LinkCheckerAnalyzer(
        IContentLoader contentLoader,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository,
        IUrlResolver urlResolver,
        IHttpClientFactory httpClientFactory,
        LinkCheckerRepository linkCheckerRepository,
        LocalizationService localizationService,
        ILogger<LinkCheckerAnalyzer> logger)
    {
        _contentLoader = contentLoader;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
        _urlResolver = urlResolver;
        _httpClientFactory = httpClientFactory;
        _linkCheckerRepository = linkCheckerRepository;
        _localizationService = localizationService;
        _logger = logger;
    }

    public void Initialize()
    {
        _linkEntries.Clear();
        _linkCheckerRepository.Clear();
    }

    public void Analyze(IContent content, ContentReference contentRef)
    {
        var contentType = _contentTypeRepository.Load(content.ContentTypeID);
        var contentTypeName = contentType?.DisplayName ?? contentType?.Name;
        var language = (content as ILocalizable)?.Language?.Name;
        var breadcrumb = content.GetBreadcrumb();
        var editUrl = $"{Paths.ToResource("CMS", "")}#/content/{contentRef.ID}/language/{language}";

        ExtractLinksFromContent(content, contentRef, contentTypeName, breadcrumb, editUrl);
    }

    public void Complete()
    {
        // Resolve block usage
        ResolveBlockUsage();

        // Check all collected links
        foreach (var entry in _linkEntries)
        {
            try
            {
                CheckLink(entry);
                _linkCheckerRepository.Save(entry.Record);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking link {Url}", entry.Record.Url);
                entry.Record.StatusCode = 0;
                entry.Record.StatusText = "Error";
                entry.Record.IsValid = false;
                _linkCheckerRepository.Save(entry.Record);
            }
        }
    }

    private void ExtractLinksFromContent(IContent content, ContentReference contentRef,
        string? contentTypeName, string breadcrumb, string editUrl)
    {
        foreach (var prop in content.Property)
        {
            try
            {
                if (prop.Value is XhtmlString xhtml)
                {
                    var html = xhtml.ToHtmlString();
                    if (string.IsNullOrWhiteSpace(html)) continue;

                    var urls = ExtractLinksFromHtml(html);
                    foreach (var url in urls)
                    {
                        if (ShouldSkipUrl(url)) continue;

                        _linkEntries.Add(new LinkEntry
                        {
                            Record = new LinkCheckRecord
                            {
                                ContentId = contentRef.ID,
                                ContentName = content.Name,
                                ContentTypeName = contentTypeName,
                                PropertyName = prop.Name,
                                Url = url,
                                LinkType = IsExternalUrl(url) ? "External" : "Internal",
                                Breadcrumb = breadcrumb,
                                EditUrl = editUrl,
                                LastChecked = DateTime.UtcNow
                            }
                        });
                    }
                }
                else if (prop.Value is EPiServer.Url urlValue)
                {
                    var url = urlValue.ToString();
                    if (!string.IsNullOrWhiteSpace(url) && !ShouldSkipUrl(url))
                    {
                        _linkEntries.Add(new LinkEntry
                        {
                            Record = new LinkCheckRecord
                            {
                                ContentId = contentRef.ID,
                                ContentName = content.Name,
                                ContentTypeName = contentTypeName,
                                PropertyName = prop.Name,
                                Url = url,
                                LinkType = IsExternalUrl(url) ? "External" : "Internal",
                                Breadcrumb = breadcrumb,
                                EditUrl = editUrl,
                                LastChecked = DateTime.UtcNow
                            }
                        });
                    }
                }
                else if (prop.Value is ContentReference linkRef && !ContentReference.IsNullOrEmpty(linkRef)
                         && prop.Name != "PageParentLink" && prop.Name != "PageLink")
                {
                    _linkEntries.Add(new LinkEntry
                    {
                        Record = new LinkCheckRecord
                        {
                            ContentId = contentRef.ID,
                            ContentName = content.Name,
                            ContentTypeName = contentTypeName,
                            PropertyName = prop.Name,
                            Url = $"content://{linkRef.ID}",
                            LinkType = "Internal",
                            Breadcrumb = breadcrumb,
                            EditUrl = editUrl,
                            LastChecked = DateTime.UtcNow
                        },
                        ContentLinkRef = linkRef
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting links from property {PropertyName} on {ContentLink}",
                    prop.Name, contentRef);
            }
        }
    }

    private static IEnumerable<string> ExtractLinksFromHtml(string html)
    {
        var matches = Regex.Matches(html, @"href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        return matches.Select(m => m.Groups[1].Value).Distinct();
    }

    private static bool ShouldSkipUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (url.StartsWith('#')) return true;
        if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) return true;
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsExternalUrl(string url)
    {
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private void ResolveBlockUsage()
    {
        var contentIds = _linkEntries.Select(e => e.Record.ContentId).Distinct().ToList();
        var blockUsageCache = new Dictionary<int, (string names, string urls)>();

        foreach (var contentId in contentIds)
        {
            try
            {
                var contentRef = new ContentReference(contentId);
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
                    continue;

                // Only resolve for blocks (not pages or media used as pages)
                if (content is not BlockData)
                    continue;

                var references = _contentRepository.GetReferencesToContent(contentRef, false);
                var pageNames = new List<string>();
                var pageUrls = new List<string>();

                foreach (var reference in references.Take(10)) // Limit to 10 references
                {
                    try
                    {
                        if (_contentLoader.TryGet<IContent>(reference.OwnerID, out var owner) && owner is PageData)
                        {
                            pageNames.Add(owner.Name);
                            var friendlyUrl = _urlResolver.GetUrl(owner.ContentLink);
                            var ownerEditUrl = $"{Paths.ToResource("CMS", "")}#/content/{owner.ContentLink.ID}";
                            pageUrls.Add($"{owner.Name}|{friendlyUrl ?? ""}|{ownerEditUrl}");
                        }
                    }
                    catch { /* Skip inaccessible references */ }
                }

                if (pageNames.Count > 0)
                {
                    blockUsageCache[contentId] = (
                        string.Join(", ", pageNames),
                        string.Join(";;", pageUrls)
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error resolving block usage for content {ContentId}", contentId);
            }
        }

        // Apply usage info to all matching entries
        foreach (var entry in _linkEntries)
        {
            if (blockUsageCache.TryGetValue(entry.Record.ContentId, out var usage))
            {
                entry.Record.UsedOn = usage.names;
                entry.Record.UsedOnEditUrls = usage.urls;
            }
        }
    }

    private void CheckLink(LinkEntry entry)
    {
        if (entry.ContentLinkRef != null)
        {
            CheckContentReference(entry);
            return;
        }

        var url = entry.Record.Url;

        if (IsExternalUrl(url))
        {
            CheckExternalLink(entry);
        }
        else
        {
            CheckInternalLink(entry);
        }
    }

    private void CheckContentReference(LinkEntry entry)
    {
        try
        {
            var contentRef = entry.ContentLinkRef!.ToReferenceWithoutVersion();
            if (_contentLoader.TryGet<IContent>(contentRef, out var content))
            {
                entry.Record.StatusCode = 200;
                entry.Record.StatusText = "OK";
                entry.Record.IsValid = true;
                entry.Record.TargetContentId = contentRef.ID;
                try
                {
                    var friendlyUrl = _urlResolver.GetUrl(contentRef);
                    entry.Record.FriendlyUrl = friendlyUrl;
                    entry.Record.Url = $"{content.Name} (ID: {contentRef.ID})";
                }
                catch { /* Keep raw URL */ }
            }
            else
            {
                entry.Record.StatusCode = 404;
                entry.Record.StatusText = "Content Not Found";
                entry.Record.IsValid = false;
            }
        }
        catch
        {
            entry.Record.StatusCode = 404;
            entry.Record.StatusText = "Content Not Found";
            entry.Record.IsValid = false;
        }
    }

    private void CheckInternalLink(LinkEntry entry)
    {
        try
        {
            var url = entry.Record.Url;
            var urlBuilder = new EPiServer.UrlBuilder(url);
            var content = _urlResolver.Route(urlBuilder);

            if (content != null)
            {
                entry.Record.StatusCode = 200;
                entry.Record.StatusText = "OK";
                entry.Record.IsValid = true;
                entry.Record.TargetContentId = content.ContentLink.ID;
                try
                {
                    var friendlyUrl = _urlResolver.GetUrl(content.ContentLink);
                    entry.Record.FriendlyUrl = friendlyUrl ?? url;
                }
                catch { entry.Record.FriendlyUrl = url; }
            }
            else
            {
                entry.Record.StatusCode = 404;
                entry.Record.StatusText = "Not Found";
                entry.Record.IsValid = false;
                entry.Record.FriendlyUrl = url;
            }
        }
        catch
        {
            entry.Record.StatusCode = 404;
            entry.Record.StatusText = "Not Found";
            entry.Record.IsValid = false;
            entry.Record.FriendlyUrl = entry.Record.Url;
        }
    }

    private void CheckExternalLink(LinkEntry entry)
    {
        entry.Record.FriendlyUrl = entry.Record.Url; // External URLs are already friendly
        try
        {
            var client = _httpClientFactory.CreateClient("LinkChecker");
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EditorPowertools-LinkChecker/1.0");

            // Try HEAD first for efficiency
            var request = new HttpRequestMessage(HttpMethod.Head, entry.Record.Url);
            var response = client.Send(request);

            // Fall back to GET if HEAD returns 405 Method Not Allowed
            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                request = new HttpRequestMessage(HttpMethod.Get, entry.Record.Url);
                response = client.Send(request);
            }

            var statusCode = (int)response.StatusCode;
            entry.Record.StatusCode = statusCode;
            entry.Record.StatusText = response.ReasonPhrase ?? response.StatusCode.ToString();
            entry.Record.IsValid = statusCode >= 200 && statusCode < 400;

            // Small delay to avoid overwhelming servers
            Thread.Sleep(100);
        }
        catch (TaskCanceledException)
        {
            entry.Record.StatusCode = 408;
            entry.Record.StatusText = "Timeout";
            entry.Record.IsValid = false;
        }
        catch (HttpRequestException ex)
        {
            entry.Record.StatusCode = 0;
            entry.Record.StatusText = $"Connection Error: {ex.Message}";
            entry.Record.IsValid = false;
        }
        catch (Exception ex)
        {
            entry.Record.StatusCode = 0;
            entry.Record.StatusText = $"Error: {ex.Message}";
            entry.Record.IsValid = false;
        }
    }

    private class LinkEntry
    {
        public LinkCheckRecord Record { get; set; } = null!;
        public ContentReference? ContentLinkRef { get; set; }
    }
}
