using EPiServer;
using EPiServer.Core;
using EPiServer.ServiceLocation;

namespace EditorPowertools.Helpers;

public static class ContentExtensions
{
    /// <summary>
    /// Builds a breadcrumb path string for a content item.
    /// Returns "Root / Parent / Child / Item".
    /// </summary>
    public static string GetBreadcrumb(this ContentReference contentLink)
    {
        try
        {
            var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();
            if (!contentLoader.TryGet<IContent>(contentLink, out var content))
                return "[Unavailable]";

            return content.GetBreadcrumb();
        }
        catch
        {
            return "[Unavailable]";
        }
    }

    /// <summary>
    /// Builds a breadcrumb path string for a content item.
    /// </summary>
    public static string GetBreadcrumb(this IContent content)
    {
        try
        {
            var contentLoader = ServiceLocator.Current.GetInstance<IContentLoader>();
            var parts = new List<string>();
            var current = content;

            while (current != null)
            {
                parts.Insert(0, current.Name);
                if (ContentReference.IsNullOrEmpty(current.ParentLink) ||
                    current.ParentLink.CompareToIgnoreWorkID(ContentReference.RootPage))
                    break;

                if (!contentLoader.TryGet(current.ParentLink, out current))
                    break;
            }

            return string.Join(" / ", parts);
        }
        catch
        {
            return content.Name;
        }
    }
}
