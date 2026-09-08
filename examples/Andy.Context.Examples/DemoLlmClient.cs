using System.Text.Json;
using Andy.Context.Llm;
using Andy.Context.Model;
using Andy.Context.Tooling;
using Andy.Context.Utils;

namespace Andy.Context.Examples;

/// <summary>
/// A minimal demo LLM client that interprets the user's last message.
/// - If it contains the word "calc", it emits a tool call to Calculator.
/// - Otherwise, it echoes a generic assistant message.
/// This lets you exercise the orchestration without any vendor SDKs.
/// </summary>
public sealed class DemoLlmClient : ILlmClient
{
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var context = request.Messages;
        var declaredTools = request.Tools;
        var lastUser = context.LastOrDefault(m => m.Role == Role.User)?.Content ?? string.Empty;

        // If we see tool messages with results, produce a final answer that references them.
        var lastTool = context.LastOrDefault(m => m.Role == Role.Tool);
        if (lastTool != null)
        {
            var reply = new Message
            {
                Role = Role.Assistant,
                Content = $"Tool result received: {Truncate(lastTool.Content, 160)}"
            };
            return Task.FromResult(new LlmResponse { AssistantMessage = reply });
        }

        if (lastUser.Contains("calc", StringComparison.OrdinalIgnoreCase))
        {
            // Emit a tool call asking the Calculator to evaluate an expression.
            var call = new ToolCall
            {
                Id = Guid.NewGuid().ToString(),
                Name = "calculator",
                ArgumentsJson = JsonSerializer.Serialize(new { expression = "2+2*3" }, JsonOptions.Default)
            };
            var reply = new Message
            {
                Role = Role.Assistant,
                Content = "Calling calculator...",
                ToolCalls = new List<ToolCall> { call }
            };
            return Task.FromResult(new LlmResponse { AssistantMessage = reply });
        }
        else
        {
            var reply = new Message
            {
                Role = Role.Assistant,
                Content = "Hello! (demo LLM): Ask me to `calc` something to see tool calling."
            };
            return Task.FromResult(new LlmResponse { AssistantMessage = reply });
        }
    }

    public async IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For demo purposes, just yield the non-streaming response
        var response = await CompleteAsync(request, cancellationToken);
        yield return new LlmStreamResponse
        {
            Delta = response.AssistantMessage,
            IsComplete = true,
            Usage = response.Usage
        };
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, Math.Max(0, max - 3)) + "...";
    }
}