using System.Globalization;
using EPiServer;
using EPiServer.Core;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class ContentReferenceHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) => new() { Definition = null!, Property = prop };

    [Fact]
    public void CanHandle_MatchesContentAndPageReference()
    {
        var h = new ContentReferenceHandler(Mock.Of<IContentLoader>());
        h.CanHandle(Ctx(new PropertyContentReference())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyPageReference())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
    }

    [Fact]
    public void GetDisplay_ResolvesNameAndId()
    {
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Name).Returns("Hello");

        var loader = new StubContentLoader(new ContentReference(7), content.Object);
        var h = new ContentReferenceHandler(loader);
        var prop = new PropertyContentReference { Value = new ContentReference(7) };

        h.GetDisplay(Ctx(prop)).Should().Be("Hello (ID: 7)");
    }

    [Fact]
    public void TryParse_IntToContentReference()
    {
        var h = new ContentReferenceHandler(Mock.Of<IContentLoader>());
        h.TryParse("12", Ctx(new PropertyContentReference()), out var v).Should().BeTrue();
        ((ContentReference)v!).ID.Should().Be(12);
    }

    [Fact]
    public void GetEditor_IsReferencePicker()
    {
        var h = new ContentReferenceHandler(Mock.Of<IContentLoader>());
        h.GetEditor(Ctx(new PropertyContentReference()))!.Kind.Should().Be("reference");
    }

    /// <summary>
    /// Minimal IContentLoader stub. IContentLoader has ContentReference-based TryGet
    /// overloads directly on the interface (not just as extension methods), so a
    /// concrete stub can implement the relevant method without Moq limitations.
    /// </summary>
    private sealed class StubContentLoader : IContentLoader
    {
        private readonly Dictionary<int, IContent> _store = new();

        public StubContentLoader(ContentReference cr, IContent content)
        {
            _store[cr.ID] = content;
        }

        // --- TryGet (ContentReference) — the ones the handler uses ---

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
            TryGet<T>(contentLink, out content);

        public bool TryGet<T>(ContentReference contentLink, LoaderOptions settings, out T content) where T : IContentData =>
            TryGet<T>(contentLink, out content);

        // --- TryGet (Guid) ---

        public bool TryGet<T>(Guid contentGuid, out T content) where T : IContentData
        { content = default!; return false; }

        public bool TryGet<T>(Guid contentGuid, CultureInfo language, out T content) where T : IContentData
        { content = default!; return false; }

        public bool TryGet<T>(Guid contentGuid, LoaderOptions settings, out T content) where T : IContentData
        { content = default!; return false; }

        // --- Get (ContentReference) ---

        public T Get<T>(ContentReference contentLink) where T : IContentData
        {
            if (_store.TryGetValue(contentLink.ID, out var c) && c is T typed) return typed;
            throw new ContentNotFoundException(contentLink);
        }

        public T Get<T>(ContentReference contentLink, CultureInfo language) where T : IContentData =>
            Get<T>(contentLink);

        public T Get<T>(ContentReference contentLink, LoaderOptions settings) where T : IContentData =>
            Get<T>(contentLink);

        // --- Get (Guid) ---

        public T Get<T>(Guid contentGuid) where T : IContentData => throw new NotSupportedException();
        public T Get<T>(Guid contentGuid, CultureInfo language) where T : IContentData => throw new NotSupportedException();
        public T Get<T>(Guid contentGuid, LoaderOptions settings) where T : IContentData => throw new NotSupportedException();

        // --- GetItems ---

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

        // --- GetChildren ---

        public IEnumerable<T> GetChildren<T>(ContentReference contentLink) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, CultureInfo language) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, LoaderOptions settings) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, CultureInfo language, int startIndex, int maxRows) where T : IContentData => [];
        public IEnumerable<T> GetChildren<T>(ContentReference contentLink, LoaderOptions settings, int startIndex, int maxRows) where T : IContentData => [];

        // --- GetDescendents / Ancestors / BySegment ---

        public IEnumerable<ContentReference> GetDescendents(ContentReference contentLink) => [];
        public IEnumerable<IContent> GetAncestors(ContentReference contentLink) => [];
        public IContent? GetBySegment(ContentReference parentLink, string urlSegment, CultureInfo language) => null;
        public IContent? GetBySegment(ContentReference parentLink, string urlSegment, LoaderOptions settings) => null;

        // --- Misc ---

        public bool IsLoaded(ContentReference contentLink) => _store.ContainsKey(contentLink.ID);
    }
}
