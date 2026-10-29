using EPiServer.DataAbstraction;
using System.Globalization;
using EPiServer.Core;
using EPiServer.Framework.Localization;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Providers;

public class SearchResultFactoryTests
{
    private const string Dash = "—";

    [Fact]
    public void BuildTitle_Encodes() =>
        SearchResultFactory.BuildTitle("<b>A</b>", "Page").Should().Be($"&lt;b&gt;A&lt;/b&gt;  {Dash}  Page");

    [Fact]
    public void BuildTitle_NoDetail() => SearchResultFactory.BuildTitle("A", null).Should().Be("A");

    [Fact]
    public void AppendLanguage_BeforeFragment() =>
        SearchResultFactory.AppendLanguage("/ui/cms/#context=epi.cms.contentdata:///5", "sv")
            .Should().Be("/ui/cms/?language=sv#context=epi.cms.contentdata:///5");

    [Fact]
    public void AppendLanguage_Empty() => SearchResultFactory.AppendLanguage("/x", "").Should().Be("/x");

    [Fact]
    public void AppendLanguage_NoFragment() => SearchResultFactory.AppendLanguage("/x", "sv").Should().Be("/x?language=sv");

    public interface ILocalizablePage : IContent, ILocalizable { }

    private class TestFactory : SearchResultFactory
    {
        public TestFactory(IContentTypeRepository t, LocalizationService l)
            : base(t, null!, l) { }
        protected override string? ResolveTypeIdentifier(IContent content) => "episerver.core.pagedata";
        protected override string ResolveEditUrl(IContent content) => "/ui/cms/#context=x";
    }

    [Fact]
    public void Create_EmitsPascalCaseMetadata()
    {
        var page = new Mock<ILocalizablePage>();
        page.SetupGet(p => p.Name).Returns("Start <1>");
        page.SetupGet(p => p.ContentLink).Returns(new ContentReference(5, 2));
        page.SetupGet(p => p.ParentLink).Returns(new ContentReference(1));
        page.SetupGet(p => p.ContentTypeID).Returns(7);
        page.SetupGet(p => p.Language).Returns(new CultureInfo("sv"));

        var types = new Mock<IContentTypeRepository>();
        types.Setup(t => t.Load(7)).Returns((ContentType?)null!);

        var sut = new TestFactory(types.Object, new MemoryLocalizationService());
        var result = sut.Create(page.Object, isDraft: true, requestedLanguage: "en", iconCssClass: "icon");

        result.Metadata.Keys.Should().BeEquivalentTo(new[]
        {
            "Id", "ParentId", "IsOnCurrentHost", "TypeId", "TypeIdentifier",
            "ThumbnailUrl", "LanguageBranch", "IsUntranslatedContent"
        });
        result.Metadata["Id"].Should().Be("5_2");
        result.Metadata["ParentId"].Should().Be("1");
        result.Metadata["TypeId"].Should().Be("7");
        result.Metadata["TypeIdentifier"].Should().Be("episerver.core.pagedata");
        result.Metadata["LanguageBranch"].Should().Be("sv");
        result.Metadata["IsUntranslatedContent"].Should().Be("true");
        result.Title.Should().Be($"Start &lt;1&gt;  {Dash}  Unpublished draft");
        result.IconCssClass.Should().Be("icon");
        result.Url.Should().Be("/ui/cms/?language=sv#context=x");
    }
}
