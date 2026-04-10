using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.Services;

/// <summary>
/// Interface for pluggable content analyzers. Each analyzer is called once per content item
/// during the unified scheduled job traversal. Analyzers store their own results.
/// </summary>
public interface IContentAnalyzer
{
    /// <summary>Display name for progress reporting.</summary>
    string Name { get; }

    /// <summary>Called once before traversal starts. Use to clear old data, set up caches, etc.</summary>
    void Initialize();

    /// <summary>Called for each content item during traversal.</summary>
    void Analyze(IContent content, ContentReference contentRef);

    /// <summary>Called after traversal completes. Use to run external checks, save accumulated data, etc.</summary>
    void Complete();
}
