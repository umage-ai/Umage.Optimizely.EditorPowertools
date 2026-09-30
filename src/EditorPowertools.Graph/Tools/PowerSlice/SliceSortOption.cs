namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

/// <summary>One way of ordering a slice, offered to the editor in the widget.</summary>
/// <param name="Key">Passed back as the <c>sortBy</c> parameter.</param>
/// <param name="LabelPath">Localization path of the label shown in the sort dropdown.</param>
public sealed record SliceSortOption(string Key, string LabelPath)
{
    public const string Name = "name";
    public const string Changed = "changed";
    public const string ChangedAsc = "changed-asc";
    public const string Published = "published";

    public static SliceSortOption ByName { get; } = new(Name, "/editorpowertools/graph/powerslice/sort/name");
    public static SliceSortOption Newest { get; } = new(Changed, "/editorpowertools/graph/powerslice/sort/changed");
    public static SliceSortOption Oldest { get; } = new(ChangedAsc, "/editorpowertools/graph/powerslice/sort/changedasc");
    public static SliceSortOption RecentlyPublished { get; } = new(Published, "/editorpowertools/graph/powerslice/sort/published");
}
