using EPiServer.Shell.ViewComposition;

namespace EditorPowertools.Tools.ActiveEditors;

[Component]
public class ActiveEditorsComponent : ComponentDefinitionBase
{
    public ActiveEditorsComponent()
        : base("editorpowertools/ActiveEditorsWidget")
    {
        Categories = new[] { "content" };
        PlugInAreas = new[] { "/episerver/cms/assets" };
        Title = "Active Editors";
        Description = "See who else is editing this content and collaborate with your team.";
        SortOrder = 190;
    }
}
