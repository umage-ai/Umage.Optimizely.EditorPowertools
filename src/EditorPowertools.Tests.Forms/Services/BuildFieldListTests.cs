using EPiServer.Forms;
using EPiServer.Forms.Core.Models.Internal;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Forms.Services;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Forms.Services;

/// <summary>
/// Tests for <see cref="FormsAggregationService.BuildFieldList"/> — the friendly-named
/// field projection used by the Submissions Timeline detail view. Forms system columns
/// must NOT appear in the field table (they're surfaced in the metadata header instead).
/// </summary>
public class BuildFieldListTests
{
    private static string Sys(string name) => Constants.SYSTEMCOLUMN_PREFIX + name;

    [Fact]
    public void BuildFieldList_ExcludesSystemColumnsPresentInSchema()
    {
        var data = new Dictionary<string, object>
        {
            ["__field_name"] = "test",
            ["__field_email"] = "test@test.com",
            [Sys("SubmitUser")] = "Admin",
            [Sys("SubmissionId")] = "2302:abc", // un-friendly-named — used to leak a raw label
        };
        var schema = new List<FriendlyNameInfo>
        {
            new() { ElementId = "__field_name", Label = "Name" },
            new() { ElementId = "__field_email", Label = "Email" },
            new() { ElementId = Sys("SubmitUser"), FriendlyName = "By user" },
            new() { ElementId = Sys("SubmissionId") },
        };

        var result = FormsAggregationService.BuildFieldList(data, schema);

        result.Select(f => f.Key).Should().Equal("__field_name", "__field_email");
        result.Should().NotContain(f => f.Key.StartsWith(Constants.SYSTEMCOLUMN_PREFIX));
    }

    [Fact]
    public void BuildFieldList_AppendsNonSchemaLeftovers_ButNeverSystemColumns()
    {
        var data = new Dictionary<string, object>
        {
            ["__field_name"] = "x",
            ["__legacy_field"] = "captured before rename",
            [Sys("SubmitTime")] = "2026-04-29",
        };
        var schema = new List<FriendlyNameInfo>
        {
            new() { ElementId = "__field_name", Label = "Name" },
        };

        var result = FormsAggregationService.BuildFieldList(data, schema);

        result.Select(f => f.Key).Should().Contain("__legacy_field");
        result.Should().NotContain(f => f.Key.StartsWith(Constants.SYSTEMCOLUMN_PREFIX));
    }
}
