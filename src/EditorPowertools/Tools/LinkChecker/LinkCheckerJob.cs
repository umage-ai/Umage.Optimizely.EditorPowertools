using System.Net;
using System.Text.RegularExpressions;
using EditorPowertools.Helpers;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.PlugIn;
using EPiServer.Scheduler;
using EPiServer.Web.Routing;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.LinkChecker;

[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Link Checker",
    Description = "Scans all content for broken internal and external links.",
    SortIndex = 10002)]
public class LinkCheckerJob : ScheduledJobBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IUrlResolver _urlResolver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LinkCheckerRepository _linkCheckerRepository;
    private readonly ILogger<LinkCheckerJob> _logger;
    private bool _stopSignaled;

    public LinkCheckerJob(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IUrlResolver urlResolver,
        IHttpClientFactory httpClientFactory,
        LinkCheckerRepository linkCheckerRepository,
        ILogger<LinkCheckerJob> logger)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _urlResolver = urlResolver;
        _httpClientFactory = httpClientFactory;
        _linkCheckerRepository = linkCheckerRepository;
        _logger = logger;
        IsStoppable = true;
    }

    public override string Execute()
    {
        _stopSignaled = false;

        // Clear old data
        _linkCheckerRepository.Clear();

        OnStatusChanged("Fetching content tree...");

        var descendants = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var total = descendants.Count;
        var processed = 0;
        var linksFound = 0;

        OnStatusChanged($"Scanning {total} content items for links...");

        // Collect all links first
        var linkEntries = new List<LinkEntry>();

        foreach (var contentRef in descendants)
        {
            if (_stopSignaled)
                return $"Job stopped after processing {processed}/{total} items. Found {linksFound} links.";

            try
            {
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
                    continue;

                var contentType = _contentTypeRepository.Load(content.ContentTypeID);
                var contentTypeName = contentType?.DisplayName ?? contentType?.Name;
                var language = (content as ILocalizable)?.Language?.Name;
                var breadcrumb = content.GetBreadcrumb();
                var editUrl = $"/EPiServer/CMS/#/content/{contentRef.ID}/language/{language}";

                ExtractLinksFromContent(content, contentRef, contentTypeName, breadcrumb, editUrl, linkEntries);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting links from content {ContentLink}", contentRef);
            }

            processed++;
            if (processed % 100 == 0)
                OnStatusChanged($"Extracted links from {processed}/{total} content items...");
        }

        linksFound = linkEntries.Count;
        OnStatusChanged($"Found {linksFound} links. Checking status...");

        // Now check all links
        var checkedCount = 0;
        foreach (var entry in linkEntries)
        {
            if (_stopSignaled)
                return $"Job stopped after checking {checkedCount}/{linksFound} links.";

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

            checkedCount++;
            if (checkedCount % 50 == 0)
                OnStatusChanged($"Checked {checkedCount}/{linksFound} links...");
        }

        var brokenCount = linkEntries.Count(e => !e.Record.IsValid);
        return $"Completed. Scanned {processed} content items, found {linksFound} links ({brokenCount} broken).";
    }

    private void ExtractLinksFromContent(IContent content, ContentReference contentRef,
        string? contentTypeName, string breadcrumb, string editUrl, List<LinkEntry> entries)
    {
        foreach (var prop in content.Property)
        {
            if (_stopSignaled) break;

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

                        entries.Add(new LinkEntry
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
                        entries.Add(new LinkEntry
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
                    entries.Add(new LinkEntry
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

    private void CheckLink(LinkEntry entry)
    {
        if (entry.ContentLinkRef != null)
        {
            // ContentReference-based internal link
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
                // Resolve friendly URL
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
                // Resolve friendly URL
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

    public override void Stop()
    {
        _stopSignaled = true;
        base.Stop();
    }

    private class LinkEntry
    {
        public LinkCheckRecord Record { get; set; } = null!;
        public ContentReference? ContentLinkRef { get; set; }
    }
}
