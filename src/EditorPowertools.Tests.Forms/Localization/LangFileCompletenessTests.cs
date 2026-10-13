using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Forms.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Localization;

/// <summary>Guards the language files: all 11 languages ship, each mirrors the keys of en.xml, nothing is empty.</summary>
public class LangFileCompletenessTests
{
    private static readonly string[] ExpectedLanguages =
        { "en", "da", "sv", "no", "de", "fi", "fr", "es", "nl", "ja", "zh-CN" };

    private static Dictionary<string, XDocument> Load()
    {
        var asm = typeof(FormsUiStringsContributor).Assembly;
        var result = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in asm.GetManifestResourceNames()
                     .Where(n => n.Contains(".lang.", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            var doc = XDocument.Load(stream);
            result[doc.Root!.Element("language")!.Attribute("id")!.Value] = doc;
        }
        return result;
    }

    private static HashSet<string> LeafPaths(XDocument doc)
    {
        var leaves = new HashSet<string>(StringComparer.Ordinal);
        void Walk(XElement e, string prefix)
        {
            foreach (var child in e.Elements())
            {
                var path = prefix + "/" + child.Name.LocalName;
                if (child.HasElements) Walk(child, path); else leaves.Add(path);
            }
        }
        Walk(doc.Root!.Element("language")!, "");
        return leaves;
    }

    [Fact]
    public void All_11_languages_ship_as_embedded_resources()
    {
        Load().Keys.Should().BeEquivalentTo(ExpectedLanguages);
    }

    [Fact]
    public void Every_language_has_exactly_the_keys_of_en()
    {
        var files = Load();
        var expected = LeafPaths(files["en"]);
        foreach (var lang in ExpectedLanguages.Where(l => l != "en"))
        {
            LeafPaths(files[lang]).Should().BeEquivalentTo(expected, "language " + lang + " must mirror en.xml");
        }
    }

    [Fact]
    public void No_language_has_empty_leaves()
    {
        foreach (var (lang, doc) in Load())
        {
            var empty = doc.Root!.Element("language")!.Descendants()
                .Where(e => !e.HasElements && string.IsNullOrWhiteSpace(e.Value))
                .Select(e => e.Name.LocalName).ToList();
            empty.Should().BeEmpty("language " + lang);
        }
    }
}
