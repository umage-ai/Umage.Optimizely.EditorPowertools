using System.Text.Json.Serialization;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PowerSearchTab { Pages, Blocks, Media, Other }

public sealed record PowerSearchQuery(
    string? Q, PowerSearchTab Tab, int Take, int Skip,
    IReadOnlyList<string> Types, IReadOnlyList<string> Languages, IReadOnlyList<Guid> Sections);

public sealed record PowerSearchFacetValueDto(string Value, string Label, int Count, bool Selected);

public sealed record PowerSearchFacetsDto(
    IReadOnlyList<PowerSearchFacetValueDto> Types,
    IReadOnlyList<PowerSearchFacetValueDto> Languages,
    IReadOnlyList<PowerSearchFacetValueDto> Sections)
{
    public static PowerSearchFacetsDto Empty { get; } = new(
        Array.Empty<PowerSearchFacetValueDto>(), Array.Empty<PowerSearchFacetValueDto>(), Array.Empty<PowerSearchFacetValueDto>());
}

public sealed record PowerSearchHitDto(
    int ContentId, string Name, string TypeName, string Language, string Breadcrumb,
    DateTime? Changed, string? ChangedBy, string Status, bool IsDraft, string EditUrl);

/// <param name="Total">-1 means unknown (the badge is hidden), not zero.</param>
public sealed record PowerSearchTabCountDto(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] PowerSearchTab Tab, int Total);

/// <param name="State">"ok" | "unavailable" | "error"</param>
public sealed record PowerSearchResponseDto(
    IReadOnlyList<PowerSearchHitDto> Hits, int Total, int? NextSkip,
    PowerSearchFacetsDto Facets, IReadOnlyList<PowerSearchTabCountDto> Tabs, string State);

public interface IPowerSearchService
{
    Task<PowerSearchResponseDto> SearchAsync(PowerSearchQuery query, CancellationToken ct);
}
