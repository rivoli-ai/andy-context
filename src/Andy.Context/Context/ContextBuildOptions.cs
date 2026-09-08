namespace Andy.Context.Context;

/// <summary>
/// Encapsulates policies for building the next request context.
/// </summary>
public sealed class ContextBuildOptions
{
    public int TokenBudget { get; init; } = 4000; // coarse units, model-agnostic
    public int MaxRecentMessages { get; init; } = 20;
    public bool IncludeToolMessages { get; init; } = true;
    public bool IncludeSystemMessages { get; init; } = true;
    public TimeSpan MaxConversationAge { get; init; } = TimeSpan.FromHours(24);
    public bool PreserveToolCallPairs { get; init; } = true;
    public CompressionStrategy CompressionStrategy { get; init; } = CompressionStrategy.Smart;
    internal void Validate()
    {
        if (TokenBudget < 0) throw new ArgumentOutOfRangeException(nameof(TokenBudget));
        if (MaxRecentMessages < 0) throw new ArgumentOutOfRangeException(nameof(MaxRecentMessages));
        if (MaxConversationAge < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(MaxConversationAge));
        if (!Enum.IsDefined(CompressionStrategy)) throw new ArgumentOutOfRangeException(nameof(CompressionStrategy));
        if (CompressionStrategy == CompressionStrategy.Semantic)
            throw new NotSupportedException("Semantic compression is not implemented.");
    }
}