using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Forms.Localization;
using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Localization;

/// <summary>Keeps the contributors in sync with the ui sections of the Forms en.xml.</summary>
public class FormsUiStringsContributorTests
{
    private static string[] ElementNames(string section)
    {
        var asm = typeof(FormsUiStringsContributor).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith(".lang.en.xml", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        var doc = XDocument.Load(stream);
        var node = doc.Descendants("ui").Single().Element(section);
        node.Should().NotBeNull();
        return node!.Elements().Select(e => e.Name.LocalName).ToArray();
    }

    [Fact]
    public void FormsOverview_keys_match_lang_file()
    {
        IUiStringsContributor c = new FormsUiStringsContributor();
        c.Section.Should().Be("formsoverview");
        c.Keys.Should().BeEquivalentTo(ElementNames("formsoverview"));
    }

    [Fact]
    public void SubmissionsTimeline_keys_match_lang_file()
    {
        IUiStringsContributor c = new SubmissionsTimelineUiStringsContributor();
        c.Section.Should().Be("submissionstimeline");
        c.Keys.Should().BeEquivalentTo(ElementNames("submissionstimeline"));
    }
}
