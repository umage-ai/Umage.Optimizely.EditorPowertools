using EPiServer.Core;
using EPiServer.ServiceLocation;
using EPiServer.Shell;

namespace EditorPowertools.Tools.ContentDetails;

[ServiceConfiguration(typeof(ViewConfiguration))]
public class ContentDetailsViewConfiguration : ViewConfiguration<IContentData>
{
    public ContentDetailsViewConfiguration()
    {
        Key = "editorpowertools-content-details";
        Name = "Power Content Details";
        Description = "Detailed information about the selected content item";
        ControllerType = "editorpowertools/ContentDetailsWidget";
        IconClass = "epi-iconObjectPage";
        SortOrder = 200;
        Category = "content";
    }
}
