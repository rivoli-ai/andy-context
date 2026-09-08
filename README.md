# Andy.Context

A .NET 10 library for provider-neutral conversation history, tool execution, context budgeting and streaming. Runtime code uses the .NET BCL; provider adapters are supplied by your application.

## Quick start

Run the mock-provider examples without API credentials:

~~~bash
dotnet run --project examples/Andy.Context.UsageExamples/Andy.Context.UsageExamples.csproj
~~~

Inside a project referencing the library and example helpers:

~~~csharp
using Andy.Context.Model;
using Andy.Context.Tooling;
using Andy.Context.Orchestration;
using Andy.Context.Examples;

var conversation = new Conversation();
var tools = new ToolRegistry();
tools.Register(new CalculatorTool());
var assistant = new AssistantOrchestrator(conversation, tools, new DemoLlmClient());
var reply = await assistant.RunTurnAsync("calc");
Console.WriteLine(reply.Content);
~~~

The mock uses a fixed calculator expression; it does not interpret arbitrary arithmetic requests.

## History and persistence

A turn enumerates its user/system message, first assistant message, first tool-result batch, then **ContinuationMessages** in arrival order. Multiple tool rounds retain:

~~~text
User → Assistant(calls) → Tool(results) → Assistant(calls) → Tool(results) → Assistant(answer)
~~~

RunTurnAsync returns the final answer without attaching earlier tool calls. Streaming saves each assembled response after its model stream is fully consumed.

~~~csharp
using Andy.Context.Utils;

conversation.SetState("preferences", new Dictionary<string, string> { ["theme"] = "dark" });
var json = conversation.ToJson();
var restored = ConversationExtensions.FromJson(json);
var preferences = restored.GetState<Dictionary<string, string>>("preferences");
~~~

JSON restores turns, continuations, IDs, timestamps and JSON-compatible state. Runtime object types are not embedded: metadata and untyped state deserialize as JsonElement; request a compatible type with GetState<T>. Cyclic or non-serializable state is unsupported. No disk storage, encryption or persistence migrations are provided.

Conversation, turn and registry collections are mutable and **not thread-safe**. Use one owner or application synchronization. Overlapping calls on one orchestrator throw; separate orchestrators sharing a conversation still require synchronization. After manually editing an existing turn, call conversation.InvalidateCache(); treat cached lists as read-only.

## Context policies

~~~csharp
using Andy.Context.Context;

var context = new ContextManager(conversation).Build(new ContextBuildOptions
{
    TokenBudget = 4000,
    MaxRecentMessages = 20,
    MaxConversationAge = TimeSpan.FromHours(24),
    IncludeSystemMessages = true,
    IncludeToolMessages = true,
    PreserveToolCallPairs = true,
    CompressionStrategy = CompressionStrategy.Smart
});
~~~

| Strategy | Behavior |
|---|---|
| Smart | Drops oldest complete turns until budget and message-count limits fit; preserves order. |
| Simple | Drops oldest individual messages until limits fit. |
| None | Applies age/role filtering and pair cleanup, but bypasses budget/count limits. |
| Semantic | Throws NotSupportedException; semantic summarization is not implemented. |

Both compressors filter message timestamps against current UTC time. Pair preservation removes incomplete tool exchanges after filtering or pruning. Excluding tool messages also removes associated call messages. Preservation does not promise to retain every historical exchange.

TokenEstimator.Estimate includes UTF-8 content bytes, serialized tool calls/results and framing. It is an estimated message budget, not a provider token count. Adapters must also account for declared tool schemas, wire format and output-token reserves. Tool JSON is never truncated.

Limits are nonnegative. Zero budgets/counts can produce empty context. System prompts have no exemption. If the newest complete turn cannot fit, Smart can return an empty list; the orchestrator rejects empty requests before calling the provider. Increase limits for large results or long multi-round turns.

## Tools

~~~csharp
using Andy.Context.Model;
using Andy.Context.Tooling;

public sealed class EchoTool : ITool
{
    public ToolDeclaration Definition { get; } = new()
    {
        Name = "echo",
        Description = "Echo text",
        Parameters = new()
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["text"] = new Dictionary<string, object> { ["type"] = "string" }
            },
            ["required"] = new[] { "text" },
            ["additionalProperties"] = false
        }
    };

    public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ToolResult.FromObject(call.Id, call.Name,
            new { text = call.ArgumentsAsJsonElement().GetProperty("text").GetString() }));
    }
}
~~~

Both orchestration modes share validation, unknown-tool and error handling. Tools execute **sequentially in request order**. Caller-managed Task.WhenAll examples do not change that contract. Ordinary tool exceptions become error results; cancellation propagates. Tools must return the matching call ID/name.

OrchestrationOptions.MaxToolRounds defaults to 8. Exceeding it records matching errors for pending calls and throws. LlmConfig forwards provider configuration with every request. Retryable exceptions are reported as tool errors; automatic retries are not implemented.

The validator implements a [documented JSON Schema subset](docs/tool-validation.md); unsupported keywords fail explicitly.

## LLM adapters and streaming

Implement ILlmClient in Andy.Context.Llm:

~~~csharp
Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request, CancellationToken cancellationToken);
~~~

Adapters translate Andy.Context.Model.Message and tool declarations into provider-specific requests.

~~~csharp
await foreach (var delta in assistant.RunTurnStreamAsync("hello"))
    Console.Write(delta.Content);
~~~

Content and arguments must be **deltas**, not repeated cumulative snapshots. Every tool fragment must carry the same stable call ID. Supply the complete name on the first fragment; later fragments may omit or repeat that name. Set ArgumentsJson to an empty string when no new arguments arrive. Interleaved calls accumulate by ID.

Usage-only chunks are skipped. A non-null Error throws, and a stream without a message throws. Consume the raw ILlmClient stream to access usage. IsComplete is advisory; enumeration ending completes accumulation. Consume the whole iterator to persist a response and execute its tools. Cancellation, provider failure or early disposal can leave a partial turn: completed batches remain, but an interrupted response is neither saved nor rolled back.

## Development and project status

See [development instructions](docs/development.md), [usage examples](examples/Andy.Context.UsageExamples/README.md), and [the remediation plan](docs/remediation-plan.md).

The September 2026 review fixes are implemented in P1-first order: 166 tests pass and library line coverage is 95.5%. GitHub issues #4–#15 track acceptance and integration. Tests exercise restored data, outgoing requests, multi-round tools, cancellation, fragmented streams, schema rejection and context limits. They use mock providers; live provider interoperability is not certified.

Semantic summarization, provider adapters, durable storage and performance benchmarks remain future work. Basic statistics and summaries are available through Andy.Context.Utils.

Repository license: Apache-2.0 — see [LICENSE](LICENSE). Existing file-level notices are preserved.
