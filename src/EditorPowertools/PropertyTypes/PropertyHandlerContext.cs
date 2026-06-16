using System.Reflection;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes;

/// <summary>Everything a property-type handler needs to inspect a property.</summary>
public sealed class PropertyHandlerContext
{
    public required PropertyDefinition Definition { get; init; }
    public Type? ModelType { get; init; }
    public PropertyInfo? ModelProperty { get; init; }
    public PropertyData? Property { get; init; }

    /// <summary>The PropertyData CLR type (from the instance if present, else the definition).</summary>
    public Type PropertyClrType => Property?.GetType() ?? Definition.Type.DefinitionType;

    public PropertyDataType DataType => Property?.Type ?? Definition.Type.DataType;

    public object? Value => Property?.Value;

    public static PropertyHandlerContext ForDefinition(PropertyDefinition definition, Type? modelType) => new()
    {
        Definition = definition,
        ModelType = modelType,
        ModelProperty = modelType?.GetProperty(definition.Name)
    };

    public static PropertyHandlerContext ForProperty(PropertyData property, PropertyDefinition definition, Type? modelType) => new()
    {
        Definition = definition,
        ModelType = modelType,
        ModelProperty = modelType?.GetProperty(definition.Name),
        Property = property
    };
}
