using System.Reflection;
using System.Xml.Linq;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace EditorPowertools.Tests.Localization;

/// <summary>Guards the language files: all 11 languages ship, each mirrors the keys of en.xml, nothing is empty.</summary>
public class LangFileCompletenessTests
{
    private static readonly string[] ExpectedLanguages =
        { "en", "da", "sv", "no", "de", "fi", "fr", "es", "nl", "ja", "zh-CN" };

    private static Dictionary<string, XDocument> Load()
    {
        var asm = typeof(IUiStringsContributor).Assembly;
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

    /// <summary>
    /// Known gap: the core ui/overview strings exist only in en.xml, so the other ten languages fall back
    /// to English for them. Ratchet: the gap may shrink, never grow. Lower this to 0 once translated.
    /// </summary>
    private const int KnownMissingPerLanguage = 213;

    [Fact]
    public void No_language_has_keys_en_lacks_and_the_translation_gap_does_not_grow()
    {
        var files = Load();
        var expected = LeafPaths(files["en"]);
        foreach (var lang in ExpectedLanguages.Where(l => l != "en"))
        {
            var actual = LeafPaths(files[lang]);
            actual.Except(expected).Should().BeEmpty("language " + lang + " must not have keys en.xml lacks");
            expected.Except(actual).Count().Should().BeLessThanOrEqualTo(KnownMissingPerLanguage, "language " + lang + " must not lose translations");
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
