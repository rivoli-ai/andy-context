using Andy.Context.Model;
using Andy.Context.Tooling;

namespace Andy.Context.Llm;

/// <summary>
/// Request structure for LLM completions.
/// </summary>
public sealed class LlmRequest
{
    /// <summary>
    /// The conversation context messages.
    /// </summary>
    public required IReadOnlyList<Message> Messages { get; init; }

    /// <summary>
    /// Available tools/functions that can be called.
    /// </summary>
    public IReadOnlyList<ToolDeclaration> Tools { get; init; } = Array.Empty<ToolDeclaration>();

    /// <summary>
    /// Optional configuration for the request.
    /// </summary>
    public LlmClientConfig? Config { get; init; }
}