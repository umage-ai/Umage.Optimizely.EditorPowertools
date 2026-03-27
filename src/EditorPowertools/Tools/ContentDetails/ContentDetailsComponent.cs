using EPiServer.Shell.ViewComposition;

namespace EditorPowertools.Tools.ContentDetails;

/// <summary>
/// Registers Power Content Details as a component in the CMS assets panel.
/// </summary>
[Component]
public class ContentDetailsComponent : ComponentDefinitionBase
{
    public ContentDetailsComponent()
        : base("editorpowertools/ContentDetailsWidget")
    {
        Categories = new[] { "content" };
        PlugInAreas = new[] { "/episerver/cms/assets" };
        Title = "Power Content Details";
        Description = "Detailed information about the selected content item - properties, references, versions.";
        SortOrder = 200;
    }
}
