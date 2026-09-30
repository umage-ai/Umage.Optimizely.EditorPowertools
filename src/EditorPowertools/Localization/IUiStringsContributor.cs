namespace UmageAI.Optimizely.EditorPowerTools.Localization;

/// <summary>
/// Lets add-on packages publish their JavaScript UI strings through <c>window.EPT_STRINGS</c>.
/// Each key resolves from <c>/editorpowertools/ui/{Section}/{key}</c>; JS reads it as
/// <c>EPT.s('{Section}.{key with / replaced by _}')</c>.
/// </summary>
public interface IUiStringsContributor
{
    string Section { get; }
    IEnumerable<string> Keys { get; }
}
