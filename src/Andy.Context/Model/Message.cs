namespace Andy.Context.Model;

/// <summary>
/// Simple message type. Content is plain text; ToolCalls/ToolResults
/// are carried separately to stay vendor-agnostic.
/// </summary>
public sealed class Message
{
    public Role Role { get; init; }
    public string Content { get; init; } = string.Empty;

    // When the assistant asks to call tools, it attaches one or more tool calls.
    public List<ToolCall> ToolCalls { get; init; } = new();

    // When tools produce results, they surface as ToolResult messages
    // (with Role = Tool) and optionally attach metadata.
    public List<ToolResult> ToolResults { get; init; } = new();

    // Arbitrary tags/metadata for routing, grounding, etc.
    public Dictionary<string, object> Metadata { get; init; } = new();

    // Timestamp for ordering/debugging
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // Message ID for referencing
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    // Optional: Message threading/parent relationships
    public string? ParentMessageId { get; init; }

    public override string ToString() => $"[{Role}] {Content}";
}