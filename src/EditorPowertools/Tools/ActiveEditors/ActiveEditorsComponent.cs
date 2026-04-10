using EPiServer.Shell.ViewComposition;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors;

[Component]
public class ActiveEditorsComponent : ComponentDefinitionBase
{
    public ActiveEditorsComponent()
        : base("editorpowertools/ActiveEditorsWidget")
    {
        Categories = new[] { "content" };
        PlugInAreas = new[] { "/episerver/cms/assets" };
        LanguagePath = "/editorpowertools/components/activeeditors";
        SortOrder = 190;
    }
}
