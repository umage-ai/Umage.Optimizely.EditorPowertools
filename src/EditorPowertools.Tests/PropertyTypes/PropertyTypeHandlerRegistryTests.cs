using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class PropertyTypeHandlerRegistryTests
{
    private sealed class StubHandler : IPropertyTypeHandler
    {
        private readonly bool _matches;
        public StubHandler(int priority, bool matches) { Priority = priority; _matches = matches; }
        public int Priority { get; }
        public bool CanHandle(PropertyHandlerContext ctx) => _matches;
        public string GetDisplay(PropertyHandlerContext ctx) => Priority.ToString();
        public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => null;
        public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value) { value = null; return false; }
    }

    [Fact]
    public void Resolve_ReturnsHighestPriorityMatch()
    {
        var registry = new PropertyTypeHandlerRegistry(new IPropertyTypeHandler[]
        {
            new StubHandler(0, matches: true),
            new StubHandler(50, matches: true),
            new StubHandler(100, matches: false),
        });

        var handler = registry.Resolve(null!);

        handler.Priority.Should().Be(50); // 100 doesn't match; 50 beats 0
    }
}
