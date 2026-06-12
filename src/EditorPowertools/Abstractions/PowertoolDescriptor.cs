using Microsoft.AspNetCore.Http;

namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

/// <summary>
/// Default <see cref="IPowertoolDescriptor"/> implementation. Use this with a
/// factory function in DI registration rather than implementing the interface
/// for each tool.
/// </summary>
public sealed class PowertoolDescriptor : IPowertoolDescriptor
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string IconSvgPath { get; init; } = string.Empty;
    public string Group { get; init; } = "Tools";
    public int SortIndex { get; init; } = 100;
    public Func<HttpContext, bool>? AvailabilityCheck { get; init; }

    public bool IsAvailable(HttpContext context) =>
        AvailabilityCheck?.Invoke(context) ?? true;
}

/// <summary>
/// Well-known group names used by the Overview. Custom groups are allowed but
/// these are the ones the built-in Overview view orders first.
/// </summary>
public static class PowertoolGroups
{
    public const string ContentEditorial = "ContentEditorial";
    public const string AuditsAnalysis   = "AuditsAnalysis";
    public const string Forms            = "Forms";
    public const string ConfigurationAdmin = "ConfigurationAdmin";
}
