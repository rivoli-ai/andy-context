namespace Andy.Context.Model;

/// <summary>
/// A single conversational turn, which can include: user message, assistant reply,
/// tool calls, tool results. Normalized structure lets us rebuild vendor-specific
/// request payloads later.
/// </summary>
public sealed class Turn
{
    public Message UserOrSystemMessage { get; init; } = new() { Role = Role.User, Content = string.Empty };
    public Message? AssistantMessage { get; set; } // may be null until produced
    public List<Message> ToolMessages { get; init; } = new(); // each with Role=Tool

    /// <summary>Messages after the first batch of tool results, in arrival order.</summary>
    public List<Message> ContinuationMessages { get; init; } = new();

    public IEnumerable<Message> EnumerateMessages()
    {
        yield return UserOrSystemMessage;
        if (AssistantMessage != null) yield return AssistantMessage;
        foreach (var t in ToolMessages) yield return t;
        foreach (var message in ContinuationMessages) yield return message;
    }
}