using Andy.Context.Model;

namespace Andy.Context.Llm;

/// <summary>
/// Response from LLM, which may contain an assistant message and/or tool calls.
/// </summary>
public sealed class LlmResponse
{
    public Message AssistantMessage { get; init; } = new() { Role = Role.Assistant, Content = string.Empty };
    public bool HasToolCalls => AssistantMessage.ToolCalls.Count > 0;
    
    // Optional: Usage information
    public LlmUsage? Usage { get; init; }
}