using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Localization;

/// <summary>
/// Guards the Graph language files: all 11 languages ship, every language has exactly the keys
/// of en.xml, nothing is empty, and every key the source references exists.
/// </summary>
public class LangFileCompletenessTests
{
    private static readonly string[] ExpectedLanguages =
        { "en", "da", "sv", "no", "de", "fi", "fr", "es", "nl", "ja", "zh-CN" };

    private static readonly Regex KeyLiteral = new("\"(/editorpowertools/[A-Za-z0-9/_-]+)\"", RegexOptions.Compiled);

    private static Dictionary<string, XDocument> LoadLangFiles(Assembly asm)
    {
        var result = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in asm.GetManifestResourceNames()
                     .Where(n => n.Contains(".lang.", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = asm.GetManifestResourceStream(name)!;
            var doc = XDocument.Load(stream);
            var id = doc.Root!.Element("language")!.Attribute("id")!.Value;
            result[id] = doc;
        }
        return result;
    }

    private static Dictionary<string, XDocument> GraphFiles() => LoadLangFiles(typeof(GraphUiStringsContributor).Assembly);

    private static XElement LanguageRoot(XDocument doc) => doc.Root!.Element("language")!;

    /// <summary>Slash-separated paths (relative to the language element) of every element.</summary>
    private static (HashSet<string> All, HashSet<string> Leaves) Paths(XDocument doc)
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        var leaves = new HashSet<string>(StringComparer.Ordinal);
        void Walk(XElement e, string prefix)
        {
            foreach (var child in e.Elements())
            {
                var path = prefix + "/" + child.Name.LocalName;
                all.Add(path);
                if (!child.HasElements) leaves.Add(path);
                else Walk(child, path);
            }
        }
        Walk(LanguageRoot(doc), "");
        return (all, leaves);
    }

    private static string FindSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var nested = Path.Combine(dir.FullName, "src", "EditorPowertools.Graph");
            if (File.Exists(Path.Combine(nested, "EditorPowertools.Graph.csproj"))) return nested;
            var sibling = Path.Combine(dir.FullName, "EditorPowertools.Graph");
            if (File.Exists(Path.Combine(sibling, "EditorPowertools.Graph.csproj"))) return sibling;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate src/EditorPowertools.Graph from " + AppContext.BaseDirectory);
    }

    private static bool IsBuildOrModulePath(string f)
    {
        var sep = Path.DirectorySeparatorChar;
        return f.Contains(sep + "obj" + sep) || f.Contains(sep + "bin" + sep) || f.Contains(sep + "modules" + sep);
    }

    [Fact]
    public void English_lang_file_is_present_and_non_empty()
    {
        var files = GraphFiles();
        files.Should().ContainKey("en");
        Paths(files["en"]).Leaves.Should().NotBeEmpty();
    }

    [Fact]
    public void English_leaves_are_not_empty()
    {
        var doc = GraphFiles()["en"];
        var empty = LanguageRoot(doc).Descendants()
            .Where(e => !e.HasElements && string.IsNullOrWhiteSpace(e.Value))
            .Select(e => e.Name.LocalName).ToList();
        empty.Should().BeEmpty();
    }

    [Fact]
    public void Every_key_referenced_in_source_exists_in_en()
    {
        var en = Paths(GraphFiles()["en"]).All;
        // Shared keys (menu/overview, overview groups) live in the core package's en.xml.
        var core = Paths(LoadLangFiles(typeof(EditorPowerTools.Localization.IUiStringsContributor).Assembly)["en"]).All;

        var root = FindSourceRoot();
        var scanned = 0;
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                     .Where(f => (f.EndsWith(".cs") || f.EndsWith(".cshtml")) && !IsBuildOrModulePath(f)))
        {
            foreach (Match m in KeyLiteral.Matches(File.ReadAllText(file)))
            {
                // Dynamic keys (prefix + name) are checked as a parent node; everything else must
                // resolve to a node in en.xml (or the core en.xml for shared keys).
                var path = m.Groups[1].Value.TrimEnd('/');
                scanned++;
                if (!en.Contains(path) && !core.Contains(path))
                    missing.Add(m.Groups[1].Value + "  (" + Path.GetFileName(file) + ")");
            }
        }
        scanned.Should().BeGreaterThan(0, "the source scan must find referenced keys, otherwise this test is vacuous");
        missing.Should().BeEmpty("every localization key referenced in C#/cshtml must exist in en.xml");
    }

    // ---- Assertions below need all ten translations. Expected RED until the translation step lands. ----

    [Fact]
    public void All_11_languages_ship_as_embedded_resources()
    {
        GraphFiles().Keys.Should().BeEquivalentTo(ExpectedLanguages);
    }

    [Fact]
    public void Every_language_has_exactly_the_keys_of_en()
    {
        var files = GraphFiles();
        var expected = Paths(files["en"]).Leaves;
        foreach (var lang in ExpectedLanguages.Where(l => l != "en"))
        {
            files.Should().ContainKey(lang);
            Paths(files[lang]).Leaves.Should().BeEquivalentTo(expected, "language " + lang + " must mirror en.xml");
        }
    }

    [Fact]
    public void No_language_has_empty_leaves()
    {
        foreach (var (lang, doc) in GraphFiles())
        {
            var empty = LanguageRoot(doc).Descendants()
                .Where(e => !e.HasElements && string.IsNullOrWhiteSpace(e.Value))
                .Select(e => e.Name.LocalName).ToList();
            empty.Should().BeEmpty("language " + lang);
        }
    }
}
