namespace Andy.Context.Llm;

/// <summary>
/// LLM usage information (tokens, costs, etc.).
/// </summary>
public sealed class LlmUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public decimal? Cost { get; init; }
}