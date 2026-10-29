namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>
/// Raised when a Graph query fails. The message is for logs only and must never be returned to clients.
/// </summary>
public class GraphQueryException : Exception
{
    public GraphQueryException(string message) : base(message) { }
    public GraphQueryException(string message, Exception innerException) : base(message, innerException) { }
}
