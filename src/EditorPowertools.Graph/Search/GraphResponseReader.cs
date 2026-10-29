using System.Text.Json;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

internal static class GraphResponseReader
{
    /// <summary>
    /// Parses a Graph response and returns a clone of its <c>data</c> element
    /// (<c>default</c>/Undefined when absent). Throws on invalid JSON or GraphQL errors.
    /// </summary>
    public static JsonElement ReadData(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            if (root.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                var message = first.ValueKind == JsonValueKind.Object
                    && first.TryGetProperty("message", out var m)
                    && m.ValueKind == JsonValueKind.String
                        ? m.GetString()
                        : "unknown error";
                throw new GraphQueryException($"Graph query failed: {message}");
            }

            return root.TryGetProperty("data", out var data) ? data.Clone() : default;
        }
        catch (JsonException ex)
        {
            throw new GraphQueryException("Graph returned a non-JSON response.", ex);
        }
    }
}
