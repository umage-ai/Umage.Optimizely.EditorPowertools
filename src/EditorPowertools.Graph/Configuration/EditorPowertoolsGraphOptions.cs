namespace UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;

/// <summary>Feature toggles for the Graph add-on. All default to enabled.</summary>
public class GraphFeatureToggles
{
    public bool PowerSearch { get; set; } = true;
    public bool PowerSlice { get; set; } = true;
    public bool SearchProviders { get; set; } = true;
}

public class GraphSearchOptions
{
    /// <summary>Hide the CMS built-in page/block/media search providers when Graph is available.</summary>
    public bool DisableBuiltInProviders { get; set; }
    public bool IncludeDrafts { get; set; } = true;
    public int OverfetchFactor { get; set; } = 5;
    public int MaxOverfetch { get; set; } = 250;
    public int ContentTypeFacetLimit { get; set; } = 100;
    public int SectionFacetLimit { get; set; } = 200;
    public int PowerSearchPageSize { get; set; } = 20;
}

public class PowerSliceOptions
{
    public int RecentlyChangedDays { get; set; } = 14;
    public int StaleContentMonths { get; set; } = 18;
    public int MaxVersions { get; set; } = 2000;
    public int MaxUnusedBlocksScanned { get; set; } = 5000;
}

/// <summary>Bound from <c>CodeArt:EditorPowertools:Graph</c>.</summary>
public class EditorPowertoolsGraphOptions
{
    public GraphFeatureToggles Features { get; set; } = new();
    public GraphSearchOptions Search { get; set; } = new();
    public PowerSliceOptions PowerSlice { get; set; } = new();
}
