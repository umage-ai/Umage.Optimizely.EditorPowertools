using EPiServer;
using EPiServer.DataAbstraction;
using System.Collections.Specialized;
using System.Security.Claims;
using System.Security.Principal;
using EPiServer.Cms.Shell.UI.Rest.ContentQuery;
using EPiServer.Core;
using EPiServer.Framework.Localization;
using EPiServer.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Tools.PowerSlice;

/// <summary>Shared scaffolding: a query helper that passes content through and a configurable caller.</summary>
internal sealed class SliceFixture
{
    public Mock<IContentRepository> Repository { get; } = new();
    public Mock<IContentQueryHelper> Helper { get; } = PassThroughHelper();
    public Mock<GraphFeatureAccessChecker> Access { get; } =
        new(Microsoft.Extensions.Options.Options.Create(new EditorPowertoolsGraphOptions()), null!);
    public Mock<IHttpContextAccessor> Http { get; } = new();
    public MemoryLocalizationService Localization { get; } = new();
    public Mock<IContentVersionRepository> Versions { get; } = new();
    public Mock<IContentLoader> Loader { get; } = new();
    public EditorPowertoolsGraphOptions Options { get; } = new();
    public ManualTimeProvider Time { get; } = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    public SliceFixture(bool allowed = true, string? user = "anna")
    {
        Access.Setup(a => a.HasUiAccess(It.IsAny<HttpContext?>(), "PowerSlice", EditorPowertoolsGraphPermissions.PowerSlice))
            .Returns(allowed);
        SetUser(user);
    }

    public IOptions<EditorPowertoolsGraphOptions> OptionsWrapper => Microsoft.Extensions.Options.Options.Create(Options);

    public void SetUser(string? user)
    {
        var identity = user is null ? new ClaimsIdentity() : new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, user) }, "Test");
        Http.Setup(h => h.HttpContext).Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
    }

    private static Mock<IContentQueryHelper> PassThroughHelper()
    {
        var helper = new Mock<IContentQueryHelper>();
        helper.Setup(h => h.FilterLanguage(It.IsAny<IEnumerable<IContent>>(), It.IsAny<bool>()))
            .Returns((IEnumerable<IContent> i, bool _) => i);
        helper.Setup(h => h.FilterTypeIdentifier(It.IsAny<IEnumerable<IContent>>(), It.IsAny<IEnumerable<string>>()))
            .Returns((IEnumerable<IContent> i, IEnumerable<string> _) => i);
        helper.Setup(h => h.FilterAccess(It.IsAny<IEnumerable<IContent>>(), It.IsAny<AccessLevel>()))
            .Returns((IEnumerable<IContent> i, AccessLevel _) => i);
        helper.Setup(h => h.FilterWasteBasket(It.IsAny<IEnumerable<IContent>>()))
            .Returns((IEnumerable<IContent> i) => i);
        return helper;
    }

    public static ContentQueryParameters Parameters(string? sortBy = null, string? q = null)
    {
        var all = new NameValueCollection();
        if (sortBy is not null) all["sortBy"] = sortBy;
        if (q is not null) all["q"] = q;
        return new ContentQueryParameters { AllParameters = all };
    }
}

internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now;
    public ManualTimeProvider(DateTimeOffset now) => _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>A minimal page-like content with a name, save date and publication date.</summary>
internal sealed class TestContent : BasicContent, IVersionable
{
    public TestContent(string name, int id, DateTime? saved = null, DateTime? startPublish = null)
    {
        Name = name;
        Saved = saved ?? DateTime.MinValue;
        StartPublish = startPublish;
        ContentLink = new ContentReference(id);
    }

    public VersionStatus Status { get; set; } = VersionStatus.Published;
    public bool IsPendingPublish { get; set; }
    public DateTime? StartPublish { get; set; }
    public DateTime? StopPublish { get; set; }
#if OPTIMIZELY_CMS13
    public string? Variation { get; set; }
#endif
}

/// <summary>Reaches the protected query pipeline of a slice by reflection.</summary>
internal static class SlicePipeline
{
    private static T Call<T>(object slice, string method, params object[] args)
    {
        var m = typeof(ContentQueryBase).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!;
        return (T)m.Invoke(slice, args)!;
    }

    public static IEnumerable<IContent> GetContent(ContentQueryBase slice, ContentQueryParameters p) =>
        Call<IEnumerable<IContent>>(slice, "GetContent", p).ToList();

    public static IEnumerable<IContent> Filter(ContentQueryBase slice, IEnumerable<IContent> items, ContentQueryParameters p) =>
        Call<IEnumerable<IContent>>(slice, "Filter", items, p).ToList();

    public static IEnumerable<IContent> Sort(ContentQueryBase slice, IEnumerable<IContent> items, ContentQueryParameters p) =>
        Call<IEnumerable<IContent>>(slice, "Sort", items, p).ToList();
}
