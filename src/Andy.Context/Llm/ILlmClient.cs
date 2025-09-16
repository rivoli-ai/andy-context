using Andy.Context.Model;
using Andy.Context.Tooling;

namespace Andy.Context.Llm;

/// <summary>
/// Vendor-agnostic interface for chat completions.
/// The vendor adapter is responsible for translating our neutral ChatMessage
/// list + tool declarations into the specific wire format (OpenAI, Azure, etc.).
/// </summary>
public interface ILlmClient
{
    Task<LlmResponse> ChatAsync(
        IReadOnlyList<Message> context,
        IReadOnlyList<ToolDeclaration> declaredTools,
        CancellationToken ct = default);
    
    // Streaming support
    IAsyncEnumerable<Message> ChatStreamAsync(
        IReadOnlyList<Message> context,
        IReadOnlyList<ToolDeclaration> declaredTools,
        CancellationToken ct = default);
}