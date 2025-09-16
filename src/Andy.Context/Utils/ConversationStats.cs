namespace Andy.Context.Utils;

/// <summary>
/// Conversation statistics and metrics.
/// </summary>
public sealed class ConversationStats
{
    public int TotalTurns { get; init; }
    public int TotalMessages { get; init; }
    public int UserMessages { get; init; }
    public int AssistantMessages { get; init; }
    public int ToolMessages { get; init; }
    public int SystemMessages { get; init; }
    public int ToolCalls { get; init; }
    public int ToolResults { get; init; }
    public int ToolErrors { get; init; }
    public DateTimeOffset? FirstMessageAt { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
    public TimeSpan? Duration => LastMessageAt - FirstMessageAt;
}