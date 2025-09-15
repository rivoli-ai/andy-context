using Andy.Engine.Util;

namespace Andy.Engine.Context;

/// <summary>
/// Represents a single entry in the context history
/// </summary>
public class ContextEntry
{
    public ContextEntry(MessageRole role, string content, DateTime timestamp)
    {
        Guard.NullOrEmpty(content, nameof(content));
        
        Role = role;
        Content = content;
        Timestamp = timestamp;
    }
    
    public MessageRole Role { get; set; }
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public int TokenEstimate { get; set; }
    public string? ToolId { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolResult { get; set; }
    public List<ContextToolCall>? ToolCalls { get; set; }
}