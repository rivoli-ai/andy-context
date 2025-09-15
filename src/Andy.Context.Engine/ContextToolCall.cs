namespace Andy.Engine.Context;

/// <summary>
/// Represents a tool call in the conversation
/// </summary>
public class ContextToolCall
{
    public string CallId { get; set; } = Guid.NewGuid().ToString("N");
    public string ToolId { get; set; } = "";
    public Dictionary<string, object?> Parameters { get; set; } = new();
}