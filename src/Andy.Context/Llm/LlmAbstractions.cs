namespace Andy.Context.Llm;

#region LLM Abstractions

/// <summary>
/// LLM client configuration.
/// </summary>
public sealed class LlmClientConfig
{
    public string Model { get; init; } = string.Empty;
    public decimal Temperature { get; init; } = 0.7m;
    public int MaxTokens { get; init; } = 4000;
    public decimal TopP { get; init; } = 1.0m;
    public int FrequencyPenalty { get; init; } = 0;
    public int PresencePenalty { get; init; } = 0;
    public string? SystemPrompt { get; init; }
}

#endregion