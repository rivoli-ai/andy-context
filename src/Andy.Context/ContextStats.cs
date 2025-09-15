namespace Andy.Context;

/// <summary>
/// Context statistics
/// </summary>
public class ContextStats
{
    public int MessageCount { get; set; }
    public int EstimatedTokens { get; set; }
    public int ToolCallCount { get; set; }
    public DateTime? OldestMessage { get; set; }
    public DateTime? NewestMessage { get; set; }
}