using EPiServer;
using EPiServer.DataAbstraction;
using EPiServer.Cms.Shell.UI.Rest.ContentQuery;
using EPiServer.Core;
using EPiServer.Framework.Localization;
using EPiServer.Shell.ContentQuery;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Tools.PowerSlice;

public class PowerSliceComponentTests
{
    private sealed class FakeSlice : PowerSliceBase
    {
        private readonly string _name;
        private readonly int _order;
        private readonly string _displayPath;

        public FakeSlice(SliceFixture f, string name, int order, string displayPath)
            : base(f.Repository.Object, f.Helper.Object, f.Access.Object, f.Http.Object, f.Localization)
        {
            _name = name;
            _order = order;
            _displayPath = displayPath;
        }

        public override string Name => _name;
        public override int SortOrder => _order;
        protected override string DisplayNamePath => _displayPath;
        protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters) => Array.Empty<IContent>();
    }

    private static object? Setting(PowerSliceComponent c, string key) =>
        c.Settings.Single(s => s.Key == key).Value;

    [Fact]
    public void Settings_list_slices_ordered_with_localized_sort_labels()
    {
        var f = new SliceFixture();
        f.Localization.AddString(System.Globalization.CultureInfo.CurrentUICulture, "/s/b", "Bravo");
        f.Localization.AddString(System.Globalization.CultureInfo.CurrentUICulture, "/s/a", "Alpha");
        f.Localization.AddString(System.Globalization.CultureInfo.CurrentUICulture, "/editorpowertools/graph/powerslice/sort/name", "By name");
        f.Localization.AddString(System.Globalization.CultureInfo.CurrentUICulture, "/editorpowertools/graph/powerslice/sort/changed", "Newest");
        IContentQuery[] queries =
        {
            new FakeSlice(f, "second", 20, "/s/b"),
            new FakeSlice(f, "first", 10, "/s/a"),
        };

        var component = new PowerSliceComponent(queries, f.Localization, f.Access.Object, f.Http.Object);

        var slices = ((System.Collections.IEnumerable)Setting(component, "slices")!).Cast<object>().ToList();
        slices.Select(s => (string)s.GetType().GetProperty("name")!.GetValue(s)!).Should().Equal("first", "second");
        slices.Select(s => (string)s.GetType().GetProperty("displayName")!.GetValue(s)!).Should().Equal("Alpha", "Bravo");

        var options = ((System.Collections.IEnumerable)slices[0].GetType().GetProperty("sortOptions")!.GetValue(slices[0])!).Cast<object>().ToList();
        options.Select(o => (string)o.GetType().GetProperty("key")!.GetValue(o)!).Should().Equal("name", "changed");
        options.Select(o => (string)o.GetType().GetProperty("label")!.GetValue(o)!).Should().Equal("By name", "Newest");
    }

    [Fact]
    public void Settings_carry_widget_strings_and_enabled_flag()
    {
        var f = new SliceFixture(allowed: false);

        var component = new PowerSliceComponent(Array.Empty<IContentQuery>(), f.Localization, f.Access.Object, f.Http.Object);

        Setting(component, "enabled").Should().Be(false);
        var strings = Setting(component, "strings")!;
        strings.GetType().GetProperty("notAvailable").Should().NotBeNull();
        strings.GetType().GetProperty("filterPlaceholder").Should().NotBeNull();
        component.LanguagePath.Should().Be("/editorpowertools/components/powerslice");
        component.PlugInAreas.Should().Contain(EPiServer.Shell.PlugInArea.AssetsDefaultGroup);
    }
}
