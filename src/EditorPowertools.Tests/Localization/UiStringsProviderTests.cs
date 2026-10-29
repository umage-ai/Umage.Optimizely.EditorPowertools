using System.Globalization;
using EPiServer.Framework.Localization;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace EditorPowertools.Tests.Localization;

public class UiStringsProviderTests
{
    private sealed class TestContributor : IUiStringsContributor
    {
        public string Section => "graphtest";
        public IEnumerable<string> Keys => new[] { "hello", "nested/key" };
    }

    [Fact]
    public void Merges_contributor_sections_with_core_sections()
    {
        var loc = new MemoryLocalizationService();
        loc.AddString(CultureInfo.CurrentUICulture, "/editorpowertools/ui/graphtest/hello", "Hi");
        var provider = new UiStringsProvider(loc, new[] { new TestContributor() });

        var all = provider.GetAll();

        all.Should().ContainKey("shared");
        var section = all["graphtest"].Should().BeAssignableTo<IDictionary<string, string>>().Subject;
        section["hello"].Should().Be("Hi");
        section.Should().ContainKey("nested_key");
    }

    [Fact]
    public void Contributor_cannot_overwrite_core_section()
    {
        var hijack = new Mock<IUiStringsContributor>();
        hijack.SetupGet(c => c.Section).Returns("shared");
        hijack.SetupGet(c => c.Keys).Returns(new[] { "loading" });
        var all = new UiStringsProvider(new MemoryLocalizationService(), new[] { hijack.Object }).GetAll();
        all["shared"].Should().NotBeAssignableTo<IDictionary<string, string>>();
    }
}
