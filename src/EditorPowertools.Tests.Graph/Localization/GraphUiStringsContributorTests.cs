using System.Xml.Linq;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Localization;
using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Localization;

/// <summary>Keeps the contributor in sync with the ui/powersearch section of the Graph en.xml.</summary>
public class GraphUiStringsContributorTests
{
    [Fact]
    public void PowerSearch_keys_match_lang_file()
    {
        var asm = typeof(GraphUiStringsContributor).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith(".lang.en.xml", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        var node = XDocument.Load(stream).Descendants("ui").Single().Element("powersearch");
        node.Should().NotBeNull();

        IUiStringsContributor c = new GraphUiStringsContributor();
        c.Section.Should().Be("powersearch");
        c.Keys.Should().BeEquivalentTo(node!.Elements().Select(e => e.Name.LocalName));
    }
}
