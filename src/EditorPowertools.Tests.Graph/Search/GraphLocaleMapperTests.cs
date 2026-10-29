using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace EditorPowertools.Tests.Graph.Search;

public class GraphLocaleMapperTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("EN", "en")]
    [InlineData("en-GB", "en_GB")]
    [InlineData("fr-ch", "fr_CH")]
    [InlineData("zh-CN", "zh_CN")]
    [InlineData("zh-Hans", "zh_Hans")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Maps_culture_names_without_truncating_regions(string? input, string? expected) =>
        GraphLocaleMapper.ToGraphLocale(input).Should().Be(expected);
}
