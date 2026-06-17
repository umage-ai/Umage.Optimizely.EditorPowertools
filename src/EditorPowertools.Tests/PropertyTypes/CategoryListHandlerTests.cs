using EPiServer.Core;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class CategoryListHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) =>
        PropertyHandlerContext.ForProperty(prop);

    // ── CanHandle ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_PropertyCategory_ReturnsTrue()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.CanHandle(Ctx(new PropertyCategory())).Should().BeTrue();
    }

    [Fact]
    public void CanHandle_OtherPropertyType_ReturnsFalse()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
    }

    // ── Priority ───────────────────────────────────────────────────────────────

    [Fact]
    public void Priority_Is40()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.Priority.Should().Be(40);
    }

    // ── GetDisplay ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetDisplay_EmptyList_ReturnsEmpty()
    {
        var repo = new Mock<CategoryRepository>();
        var h = new CategoryListHandler(repo.Object);
        var prop = new PropertyCategory { Value = new CategoryList() };
        h.GetDisplay(Ctx(prop)).Should().BeEmpty();
    }

    [Fact]
    public void GetDisplay_NullValue_ReturnsEmpty()
    {
        var repo = new Mock<CategoryRepository>();
        var h = new CategoryListHandler(repo.Object);
        h.GetDisplay(Ctx(new PropertyCategory())).Should().BeEmpty();
    }

    [Fact]
    public void GetDisplay_ResolvesNamesFromRepository()
    {
        var repo = new Mock<CategoryRepository>();
        repo.Setup(r => r.Get(1)).Returns(MakeCat(1, "Events"));
        repo.Setup(r => r.Get(2)).Returns(MakeCat(2, "News"));

        var h = new CategoryListHandler(repo.Object);
        var prop = new PropertyCategory { Value = new CategoryList(new[] { 1, 2 }) };
        h.GetDisplay(Ctx(prop)).Should().Be("Events, News");
    }

    [Fact]
    public void GetDisplay_UnknownId_FallsBackToIdString()
    {
        var repo = new Mock<CategoryRepository>();
        repo.Setup(r => r.Get(99)).Returns((Category?)null!);

        var h = new CategoryListHandler(repo.Object);
        var prop = new PropertyCategory { Value = new CategoryList(new[] { 99 }) };
        h.GetDisplay(Ctx(prop)).Should().Be("99");
    }

    [Fact]
    public void GetDisplay_RepositoryThrows_FallsBackToIdString()
    {
        var repo = new Mock<CategoryRepository>();
        repo.Setup(r => r.Get(7)).Throws<InvalidOperationException>();

        var h = new CategoryListHandler(repo.Object);
        var prop = new PropertyCategory { Value = new CategoryList(new[] { 7 }) };
        h.GetDisplay(Ctx(prop)).Should().Be("7");
    }

    // ── GetEditor ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetEditor_ReturnsCategoryKind_WithMultipleTrue()
    {
        var repo = new Mock<CategoryRepository>();
        repo.Setup(r => r.GetRoot()).Returns((Category?)null!);

        var h = new CategoryListHandler(repo.Object);
        var ed = h.GetEditor(Ctx(new PropertyCategory()))!;
        ed.Kind.Should().Be("category");
        ed.Multiple.Should().BeTrue();
    }

    [Fact]
    public void GetEditor_BuildsOptionsFromCategoryTree()
    {
        var repo = new Mock<CategoryRepository>();
        var root = MakeRoot(new[]
        {
            MakeCat(10, "Events", available: true, selectable: true),
            MakeCat(20, "News",   available: true, selectable: true),
        });
        repo.Setup(r => r.GetRoot()).Returns(root);

        var h = new CategoryListHandler(repo.Object);
        var ed = h.GetEditor(Ctx(new PropertyCategory()))!;
        ed.Options.Should().HaveCount(2);
        ed.Options!.Should().ContainSingle(o => o.Value == "10" && o.Label == "Events");
        ed.Options!.Should().ContainSingle(o => o.Value == "20" && o.Label == "News");
    }

    [Fact]
    public void GetEditor_ExcludesUnavailableCategories()
    {
        var repo = new Mock<CategoryRepository>();
        var root = MakeRoot(new[]
        {
            MakeCat(10, "Events", available: false, selectable: true),
            MakeCat(20, "News",   available: true,  selectable: true),
        });
        repo.Setup(r => r.GetRoot()).Returns(root);

        var h = new CategoryListHandler(repo.Object);
        var ed = h.GetEditor(Ctx(new PropertyCategory()))!;
        ed.Options.Should().ContainSingle(o => o.Value == "20");
    }

    [Fact]
    public void GetEditor_ExcludesNonSelectableCategories()
    {
        var repo = new Mock<CategoryRepository>();
        var root = MakeRoot(new[]
        {
            MakeCat(10, "Group",  available: true, selectable: false),
            MakeCat(20, "Leaf",   available: true, selectable: true),
        });
        repo.Setup(r => r.GetRoot()).Returns(root);

        var h = new CategoryListHandler(repo.Object);
        var ed = h.GetEditor(Ctx(new PropertyCategory()))!;
        ed.Options.Should().ContainSingle(o => o.Value == "20");
    }

    [Fact]
    public void GetEditor_WalksNestedTree()
    {
        var repo = new Mock<CategoryRepository>();
        var child = MakeCat(30, "Sub", available: true, selectable: true);
        var parent = MakeCat(10, "Parent", available: true, selectable: false, children: new[] { child });
        var root = MakeRoot(new[] { parent });
        repo.Setup(r => r.GetRoot()).Returns(root);

        var h = new CategoryListHandler(repo.Object);
        var ed = h.GetEditor(Ctx(new PropertyCategory()))!;
        // Parent is not selectable → not included; child is → included
        ed.Options.Should().ContainSingle(o => o.Value == "30");
    }

    [Fact]
    public void GetEditor_RepositoryThrows_ReturnsEmptyOptions()
    {
        var repo = new Mock<CategoryRepository>();
        repo.Setup(r => r.GetRoot()).Throws<InvalidOperationException>();

        var h = new CategoryListHandler(repo.Object);
        var ed = h.GetEditor(Ctx(new PropertyCategory()))!;
        ed.Options.Should().BeEmpty();
    }

    // ── TryParse ───────────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_CommaSeparated_ParsesIds()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.TryParse("1,2,3", Ctx(new PropertyCategory()), out var value).Should().BeTrue();
        var list = (CategoryList)value!;
        list.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void TryParse_SemicolonSeparated_ParsesIds()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.TryParse("4;5", Ctx(new PropertyCategory()), out var value).Should().BeTrue();
        var list = (CategoryList)value!;
        list.Should().BeEquivalentTo(new[] { 4, 5 });
    }

    [Fact]
    public void TryParse_PipeSeparated_ParsesIds()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.TryParse("6|7", Ctx(new PropertyCategory()), out var value).Should().BeTrue();
        var list = (CategoryList)value!;
        list.Should().BeEquivalentTo(new[] { 6, 7 });
    }

    [Fact]
    public void TryParse_NullOrEmpty_ReturnsEmptyList()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());

        h.TryParse(null, Ctx(new PropertyCategory()), out var v1).Should().BeTrue();
        ((CategoryList)v1!).IsEmpty.Should().BeTrue();

        h.TryParse("", Ctx(new PropertyCategory()), out var v2).Should().BeTrue();
        ((CategoryList)v2!).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void TryParse_NonNumericTokensSkipped_ValidIdsKept()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.TryParse("1,abc,2", Ctx(new PropertyCategory()), out var value).Should().BeTrue();
        var list = (CategoryList)value!;
        list.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static Category MakeCat(int id, string name,
        bool available = true, bool selectable = true,
        IEnumerable<Category>? children = null)
    {
        var cat = new Category
        {
            ID = id,
            Name = name,
            Available = available,
            Selectable = selectable,
        };
        if (children != null)
        {
            var col = new CategoryCollection(cat);
            foreach (var child in children)
                col.Add(child);
            cat.Categories = col;
        }
        return cat;
    }

    /// <summary>Creates a root Category (ID=0, wraps top-level children).</summary>
    private static Category MakeRoot(IEnumerable<Category>? children = null)
    {
        var root = new Category { ID = 0, Name = "Root", Available = true, Selectable = false };
        var col = new CategoryCollection(root);
        if (children != null)
            foreach (var child in children)
                col.Add(child);
        root.Categories = col;
        return root;
    }
}
