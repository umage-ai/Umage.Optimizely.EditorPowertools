using EPiServer;
using EPiServer.DataAbstraction;
using EPiServer.Cms.Shell.UI.Rest.ContentQuery;
using EPiServer.Core;
using EPiServer.Security;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Tools.PowerSlice;

public class BuiltInSlicesTests
{
    private static ContentVersion Version(int id, DateTime saved) =>
        new(new ContentReference(id, 1), "v", VersionStatus.Published, saved, "x", "x", id, "en", false, false);

    private static void Serve(SliceFixture f, params (int id, DateTime saved, IContent content)[] rows)
    {
        f.Versions.Setup(v => v.List(It.IsAny<VersionFilter>(), It.IsAny<int>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Returns(rows.Select(r => Version(r.id, r.saved)).ToList());
        foreach (var r in rows)
        {
            IContent? content = r.content;
            f.Loader.Setup(l => l.TryGet(new ContentReference(r.id), out content)).Returns(true);
        }
    }

    private static T Make<T>(SliceFixture f) where T : VersionBackedSlice =>
        (T)Activator.CreateInstance(typeof(T), f.Repository.Object, f.Helper.Object, f.Access.Object, f.Http.Object,
            f.Localization, f.Versions.Object, f.Loader.Object, f.OptionsWrapper, f.Time)!;

    [Fact]
    public void MyContent_asks_the_store_for_the_current_users_versions()
    {
        var f = new SliceFixture(user: "anna");
        VersionFilter? captured = null;
        f.Versions.Setup(v => v.List(It.IsAny<VersionFilter>(), It.IsAny<int>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Callback(new ListCallback((VersionFilter filter, int _, int _, out int total) => { captured = filter; total = 0; }))
            .Returns(new List<ContentVersion>());

        SlicePipeline.GetContent(Make<MyContentSlice>(f), SliceFixture.Parameters());

        captured.Should().NotBeNull();
        captured!.ChangedBy.Should().Be("anna");
        captured.ProviderName.Should().BeEmpty();
        captured.ExcludeDeleted.Should().BeTrue();
        captured.SortDirection.Should().Be(VersionSortDirection.Descending);
    }

    private delegate void ListCallback(VersionFilter filter, int start, int max, out int total);

    [Fact]
    public void MyContent_is_empty_for_anonymous()
    {
        var f = new SliceFixture(user: null);

        SlicePipeline.GetContent(Make<MyContentSlice>(f), SliceFixture.Parameters()).Should().BeEmpty();
        f.Versions.Verify(v => v.List(It.IsAny<VersionFilter>(), It.IsAny<int>(), It.IsAny<int>(), out It.Ref<int>.IsAny), Times.Never);
    }

    [Fact]
    public void RecentlyChanged_drops_items_older_than_the_window()
    {
        var f = new SliceFixture();
        f.Options.PowerSlice.RecentlyChangedDays = 14;
        var now = f.Time.GetLocalNow().DateTime;
        Serve(f,
            (1, now.AddDays(-1), new TestContent("fresh", 1)),
            (2, now.AddDays(-13), new TestContent("edge", 2)),
            (3, now.AddDays(-15), new TestContent("old", 3)));

        SlicePipeline.GetContent(Make<RecentlyChangedSlice>(f), SliceFixture.Parameters())
            .Select(c => c.Name).Should().Equal("fresh", "edge");
    }

    // Literal values on purpose: the names are stored by the assets-pane widget and the order is UX.
    [Fact]
    public void Slices_are_named_and_ordered_per_the_plan()
    {
        var f = new SliceFixture();
        var slices = new (ContentQueryBase Slice, string Name, int Order, SliceSortOption First)[]
        {
            (Make<MyContentSlice>(f), "ept-slice-mycontent", 10, SliceSortOption.Newest),
            (Make<RecentlyChangedSlice>(f), "ept-slice-recentlychanged", 20, SliceSortOption.Newest),
            (Make<DraftsSlice>(f), "ept-slice-drafts", 30, SliceSortOption.Newest),
            (Make<AwaitingApprovalSlice>(f), "ept-slice-awaitingapproval", 40, SliceSortOption.Oldest),
            (Make<StaleContentSlice>(f), "ept-slice-stalecontent", 50, SliceSortOption.Oldest),
            (new ScanSlice(f), "ept-slice-unusedblocks", 60, SliceSortOption.Oldest),
        };

        foreach (var (slice, name, order, first) in slices)
        {
            slice.Name.Should().Be(name);
            slice.SortOrder.Should().Be(order);
            ((PowerSliceBase)slice).SortOptions[0].Should().Be(first, name);
        }
    }

    private static void CaptureFilter(SliceFixture f, out Func<(VersionFilter? Filter, int Max)> read)
    {
        VersionFilter? filter = null;
        var max = -1;
        f.Versions.Setup(v => v.List(It.IsAny<VersionFilter>(), It.IsAny<int>(), It.IsAny<int>(), out It.Ref<int>.IsAny))
            .Callback(new ListCallback((VersionFilter fl, int _, int m, out int total) => { filter = fl; max = m; total = 0; }))
            .Returns(new List<ContentVersion>());
        read = () => (filter, max);
    }

    [Fact]
    public void Drafts_asks_for_unfinished_statuses_newest_first_and_passes_max_versions()
    {
        var f = new SliceFixture();
        f.Options.PowerSlice.MaxVersions = 77;
        CaptureFilter(f, out var read);

        SlicePipeline.GetContent(Make<DraftsSlice>(f), SliceFixture.Parameters());

        var (filter, max) = read();
        filter!.Statuses.Should().BeEquivalentTo(new[] { VersionStatus.CheckedOut, VersionStatus.CheckedIn, VersionStatus.Rejected });
        filter.SortDirection.Should().Be(VersionSortDirection.Descending);
        filter.ProviderName.Should().BeEmpty();
        max.Should().Be(77);
    }

    [Fact]
    public void AwaitingApproval_asks_only_for_that_status_longest_waiting_first()
    {
        var f = new SliceFixture();
        CaptureFilter(f, out var read);

        SlicePipeline.GetContent(Make<AwaitingApprovalSlice>(f), SliceFixture.Parameters());

        var filter = read().Filter!;
        filter.Statuses.Should().Equal(VersionStatus.AwaitingApproval);
        filter.SortDirection.Should().Be(VersionSortDirection.Ascending);
        filter.ProviderName.Should().BeEmpty();
    }

    [Fact]
    public void MyContent_passes_max_versions_to_the_store()
    {
        var f = new SliceFixture(user: "anna");
        f.Options.PowerSlice.MaxVersions = 123;
        CaptureFilter(f, out var read);

        SlicePipeline.GetContent(Make<MyContentSlice>(f), SliceFixture.Parameters());

        read().Max.Should().Be(123);
    }

    [Fact]
    public void Drafts_and_AwaitingApproval_return_the_loaded_content()
    {
        var f = new SliceFixture();
        var now = f.Time.GetLocalNow().DateTime;
        Serve(f, (1, now, new TestContent("draft", 1)));

        SlicePipeline.GetContent(Make<DraftsSlice>(f), SliceFixture.Parameters()).Select(c => c.Name).Should().Equal("draft");
        SlicePipeline.GetContent(Make<AwaitingApprovalSlice>(f), SliceFixture.Parameters()).Select(c => c.Name).Should().Equal("draft");
    }

    [Fact]
    public void RecentlyChanged_window_includes_the_exact_cutoff_and_excludes_just_before()
    {
        var f = new SliceFixture();
        f.Options.PowerSlice.RecentlyChangedDays = 14;
        var cutoff = f.Time.GetLocalNow().DateTime.AddDays(-14);
        Serve(f,
            (1, cutoff, new TestContent("on", 1)),
            (2, cutoff.AddSeconds(-1), new TestContent("before", 2)));

        SlicePipeline.GetContent(Make<RecentlyChangedSlice>(f), SliceFixture.Parameters())
            .Select(c => c.Name).Should().Equal("on");
    }

    [Fact]
    public void StaleContent_boundary_is_inclusive_and_newer_rows_or_content_are_excluded()
    {
        var f = new SliceFixture();
        f.Options.PowerSlice.StaleContentMonths = 12;
        var now = f.Time.GetLocalNow().DateTime;
        var cutoff = now.AddMonths(-12);
        Serve(f,
            (1, cutoff, new TestPage("on the line", 1, cutoff)),
            (2, cutoff.AddDays(1), new TestPage("row newer than cutoff", 2, cutoff.AddYears(-1))),
            (3, cutoff.AddYears(-1), new TestPage("draft only", 3, cutoff.AddYears(-1), VersionStatus.CheckedOut)),
            (4, cutoff.AddYears(-1), new TestContent("not a page", 4, cutoff.AddYears(-1))),
            (5, cutoff.AddYears(-1), new TestPage("stale", 5, cutoff.AddYears(-1))));

        SlicePipeline.GetContent(Make<StaleContentSlice>(f), SliceFixture.Parameters())
            .Select(c => c.Name).Should().Equal("on the line", "stale");
    }

    private sealed class TestPage : PageData
    {
        public TestPage(string name, int id, DateTime saved, VersionStatus status = VersionStatus.Published)
            : base(new AccessControlList(), new PropertyDataCollection
            {
                { "PageName", new PropertyString() },
                { "PageSaved", new PropertyDate() },
                { "PageLink", new PropertyContentReference() },
                { "PageWorkStatus", new PropertyNumber() },
            })
        {
            Name = name;
            ContentLink = new ContentReference(id);
            Saved = saved;
            Status = status;
        }
    }

    [Fact]
    public void StaleContent_ignores_old_version_rows_of_recently_edited_pages()
    {
        var f = new SliceFixture();
        f.Options.PowerSlice.StaleContentMonths = 12;
        var now = f.Time.GetLocalNow().DateTime;
        // Both rows are old versions; only page 2's current content is also old.
        Serve(f,
            (1, now.AddYears(-3), new TestPage("edited yesterday", 1, now.AddDays(-1))),
            (2, now.AddYears(-2), new TestPage("really stale", 2, now.AddYears(-2))));

        SlicePipeline.GetContent(Make<StaleContentSlice>(f), SliceFixture.Parameters())
            .Select(c => c.Name).Should().Equal("really stale");
    }

    private sealed class TestBlock : BlockData, IContent
    {
        public TestBlock(int id)
        {
            ContentLink = new ContentReference(id);
            Name = "b" + id;
        }

        public string Name { get; set; }
        public ContentReference ContentLink { get; set; }
        public ContentReference ParentLink { get; set; } = ContentReference.EmptyReference;
        public Guid ContentGuid { get; set; } = Guid.NewGuid();
        public int ContentTypeID { get; set; }
        public bool IsDeleted { get; set; }
    }

    private sealed class ScanSlice : UnusedBlocksSlice
    {
        public ScanSlice(SliceFixture f) : base(f.Repository.Object, f.Helper.Object, f.Access.Object, f.Http.Object,
            f.Localization, f.Loader.Object, f.OptionsWrapper)
        {
        }

        protected override ContentReference ScanRoot => new(1000);
    }

    [Fact]
    public void UnusedBlocks_excludes_referenced_blocks_and_stops_at_the_cap()
    {
        var f = new SliceFixture();
        f.Options.PowerSlice.MaxUnusedBlocksScanned = 3;
        var children = Enumerable.Range(1, 6).Select(i => (IContent)new TestBlock(i)).ToList();
        f.Loader.Setup(l => l.GetChildren<IContent>(new ContentReference(1000))).Returns(children);
        f.Loader.Setup(l => l.GetChildren<IContent>(It.Is<ContentReference>(r => r.ID != 1000))).Returns(Array.Empty<IContent>());
        f.Repository.Setup(r => r.GetReferencesToContent(It.IsAny<ContentReference>(), false))
            .Returns(Array.Empty<ReferenceInformation>());
        f.Repository.Setup(r => r.GetReferencesToContent(new ContentReference(2), false))
            .Returns(new[] { new ReferenceInformation() });

        var result = SlicePipeline.GetContent(new ScanSlice(f), SliceFixture.Parameters());

        result.Select(c => c.Name).Should().Equal("b1", "b3");
        f.Repository.Verify(r => r.GetReferencesToContent(It.IsAny<ContentReference>(), false), Times.Exactly(3));
    }
}
