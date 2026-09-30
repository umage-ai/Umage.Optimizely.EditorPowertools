using System.Globalization;
using EPiServer;
using EPiServer.Core;
using EPiServer.Framework.Localization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Services;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Services;

/// <summary>
/// Covers the analyzer-failure isolation added after issue #61: when an analyzer's
/// Initialize() throws (e.g. a DDS Clear() timing out), it must not be handed any
/// content via Analyze()/Complete() for that run — otherwise it appends new rows on
/// top of data it never cleared.
/// </summary>
public class UnifiedContentAnalysisJobTests
{
    private readonly Mock<IContentRepository> _contentRepository = new();
    private readonly StubContentLoader _contentLoader = new();
    private readonly Mock<ILogger<UnifiedContentAnalysisJob>> _logger = new();
    private readonly Mock<LocalizationService> _localization = new();

    public UnifiedContentAnalysisJobTests()
    {
        // GetStringByCulture(path, fallback, culture) is a non-virtual convenience overload that
        // forwards to this virtual one — Moq can only intercept the virtual member.
        _localization
            .Setup(l => l.GetStringByCulture(It.IsAny<string>(), It.IsAny<FallbackBehaviors>(), It.IsAny<string>(), It.IsAny<CultureInfo>()))
            .Returns((string _, FallbackBehaviors _, string fallback, CultureInfo _) => fallback);
    }

    private UnifiedContentAnalysisJob CreateJob(IEnumerable<IContentAnalyzer> analyzers) =>
        new(_contentRepository.Object, _contentLoader, analyzers, _logger.Object, _localization.Object);

    [Fact]
    public void Execute_AnalyzerThatFailsInitialize_IsSkippedForAnalyzeAndComplete()
    {
        var contentRef = new ContentReference(5);
        _contentRepository.Setup(r => r.GetDescendents(ContentReference.RootPage)).Returns(new[] { contentRef });
        _contentLoader.Register(contentRef, Mock.Of<IContent>());

        var failing = new Mock<IContentAnalyzer>();
        failing.SetupGet(a => a.Name).Returns("Failing");
        failing.Setup(a => a.Initialize()).Throws(new InvalidOperationException("DDS delete timed out"));

        var working = new Mock<IContentAnalyzer>();
        working.SetupGet(a => a.Name).Returns("Working");

        var job = CreateJob(new[] { failing.Object, working.Object });
        var result = job.Execute();

        failing.Verify(a => a.Analyze(It.IsAny<IContent>(), It.IsAny<ContentReference>()), Times.Never);
        failing.Verify(a => a.Complete(), Times.Never);

        working.Verify(a => a.Analyze(It.IsAny<IContent>(), contentRef), Times.Once);
        working.Verify(a => a.Complete(), Times.Once);

        result.Should().Contain("Failing");
    }

    [Fact]
    public void Execute_AllAnalyzersInitializeSuccessfully_NoFailureNoted()
    {
        _contentRepository.Setup(r => r.GetDescendents(ContentReference.RootPage)).Returns(Array.Empty<ContentReference>());

        var working = new Mock<IContentAnalyzer>();
        working.SetupGet(a => a.Name).Returns("Working");

        var job = CreateJob(new[] { working.Object });
        var result = job.Execute();

        working.Verify(a => a.Complete(), Times.Once);
        result.Should().NotContain("initialization errors");
    }

    /// <summary>Minimal IContentLoader stub — mirrors the one in ContentReferenceHandlerTests.</summary>
    private sealed class StubContentLoader : IContentLoader
    {
        private readonly Dictionary<int, IContent> _store = new();

        public void Register(ContentReference cr, IContent content) => _store[cr.ID] = content;

        public bool TryGet<T>(ContentReference contentLink, out T content) where T : IContentData
        {
            if (_store.TryGetValue(contentLink.ID, out var c) && c is T typed)
            {
                content = typed;
                return true;
            }
            content = default!;
            return false;
        }

        public bool TryGet<T>(ContentReference contentLink, CultureInfo language, out T content) where T : IContentData =>
            TryGet(contentLink, out content);

        public bool TryGet<T>(ContentReference contentLink, LoaderOptions settings, out T content) where T : IContentData =>
            TryGet(contentLink, out content);

        public bool TryGet<T>(Guid contentGuid, out T content) where T : IContentData
        { content = default!; return false; }

        public bool TryGet<T>(Guid contentGuid, CultureInfo language, out T content) where T : IContentData
        { content = default!; return false; }

        public bool TryGet<T>(Guid contentGuid, LoaderOptions settings, out T content) where T : IContentData
        { content = default!; return false; }

        public T Get<T>(ContentReference contentLink) where T : IContentData
        {
            if (_store.TryGetValue(contentLink.ID, out var c) && c is T typed) return typed;
            throw new ContentNotFoundException(contentLink);
        }

        public T Get<T>(ContentReference contentLink, CultureInfo language) where T : IContentData => Get<T>(contentLink);
        public T Get<T>(ContentReference contentLink, LoaderOptions settings) where T : IContentData => Get<T>(contentLink);
        public T Get<T>(Guid contentGuid) where T : IContentData => throw new NotSupportedException();
        public T Get<T>(Guid contentGuid, CultureInfo language) where T : IContentData => throw new NotSupportedException();
        public T Get<T>(Guid contentGuid, LoaderOptions settings) where T : IContentData => throw new NotSupportedException();

        public IEnumerable<T> GetItems<T>(IEnumerable<ContentReference> contentLinks) where T : IContentData =>
            contentLinks.Select(cr => Get<T>(cr));
        public IEnumerable<T> GetItems<T>(IEnumerable<ContentReference> contentLinks, CultureInfo language) where T : IContentData =>
            contentLinks.Select(cr => Get<T>(cr));
        public IEnumerable<T> GetItems<T>(IEnumerable<ContentReference> contentLinks, LoaderOptions settings) where T : IContentData =>
            contentLinks.Select(cr => Get<T>(cr));
        public IEnumerable<IContent> GetItems(IEnumerable<ContentReference> contentLinks, CultureInfo language) =>
            contentLinks.Select(cr => Get<IContent>(cr));
        public IEnumerable<IContent> GetItems(IEnumerable<ContentReference> contentLinks, LoaderOptions settings) =>
            contentLinks.Select(cr => Get<IContent>(cr));

        public IEnumerable<T> GetChildren<T>(ContentReference contentLink) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, CultureInfo language) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, LoaderOptions settings) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, CultureInfo language, int startIndex, int maxRows) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, LoaderOptions settings, int startIndex, int maxRows) where T : IContentData => [];

        public IEnumerable<ContentReference> GetDescendents(ContentReference contentLink) => [];
        public IEnumerable<IContent> GetAncestors(ContentReference contentLink) => [];
        public IContent? GetBySegment(ContentReference parentLink, string urlSegment, CultureInfo language) => null;
        public IContent? GetBySegment(ContentReference parentLink, string urlSegment, LoaderOptions settings) => null;
    }
}
