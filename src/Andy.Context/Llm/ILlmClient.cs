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
    /// <summary>
    /// Completes a chat request and returns the full response
    /// </summary>
    Task<LlmResponse> CompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a chat request and returns a stream of response chunks
    /// </summary>
    IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(
        LlmRequest request,
        CancellationToken cancellationToken = default);
}