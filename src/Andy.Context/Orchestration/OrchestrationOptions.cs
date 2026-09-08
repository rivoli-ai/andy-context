using Andy.Context.Llm;

namespace Andy.Context.Orchestration;

/// <summary>Execution limits and optional provider request configuration.</summary>
public sealed class OrchestrationOptions
{
    /// <summary>Maximum tool batches per turn. Tools in each batch run sequentially.</summary>
    public int MaxToolRounds { get; init; } = 8;
    /// <summary>Optional configuration forwarded to every LLM request.</summary>
    public LlmClientConfig? LlmConfig { get; init; }
}
