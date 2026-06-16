namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes;

/// <summary>Describes the inline editor the Bulk Property Editor should render for a property.</summary>
public sealed class PropertyEditorDescriptor
{
    /// <summary>text | number | bool | date | url | reference | select | multiselect | category</summary>
    public required string Kind { get; init; }

    /// <summary>Options for select/multiselect/category editors; null otherwise.</summary>
    public IReadOnlyList<EditorOption>? Options { get; init; }

    /// <summary>True when the editor accepts multiple values.</summary>
    public bool Multiple { get; init; }
}

public sealed record EditorOption(string Value, string Label);
