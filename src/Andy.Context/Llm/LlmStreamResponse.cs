using Andy.Context.Model;

namespace Andy.Context.Llm;

/// <summary>
/// Streaming response chunk from an LLM completion.
/// </summary>
public sealed class LlmStreamResponse
{
    /// <summary>
    /// Partial message content for this chunk.
    /// </summary>
    public Message? Delta { get; init; }

    /// <summary>
    /// Whether this is the final chunk in the stream.
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Usage information (typically only available in the final chunk).
    /// </summary>
    public LlmUsage? Usage { get; init; }

    /// <summary>
    /// Any error that occurred during streaming.
    /// </summary>
    public string? Error { get; init; }
}