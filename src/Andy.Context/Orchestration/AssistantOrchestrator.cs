using System.Runtime.CompilerServices;
using Andy.Context.Context;
using Andy.Context.Llm;
using Andy.Context.Model;
using Andy.Context.Tooling;

namespace Andy.Context.Orchestration;

/// <summary>
/// Builds context, records model replies, and executes sequential tool batches until a final answer.
/// One caller at a time may use an orchestrator or its conversation.
/// </summary>
public sealed class AssistantOrchestrator
{
    private readonly Conversation _conversation;
    private readonly ContextManager _contextManager;
    private readonly ToolRegistry _tools;
    private readonly ILlmClient _llm;
    private readonly OrchestrationOptions _execution;
    private int _running;

    public AssistantOrchestrator(Conversation conversation, ToolRegistry tools, ILlmClient llm,
        OrchestrationOptions? execution = null)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _contextManager = new ContextManager(conversation);
        _execution = execution ?? new();
        if (_execution.MaxToolRounds < 0) throw new ArgumentOutOfRangeException(nameof(execution));
    }

    public async Task<Message> RunTurnAsync(string userText, ContextBuildOptions? options = null, CancellationToken ct = default)
    {
        options ??= new();
        Enter(userText, options, ct);
        try
        {
            var turn = StartTurn(userText);
            var declarations = _tools.GetDeclaredTools();
            for (var round = 0; ; round++)
            {
                ct.ThrowIfCancellationRequested();
                var reply = await _llm.CompleteAsync(Request(options, declarations), ct);
                ct.ThrowIfCancellationRequested();
                RecordAssistant(turn, reply.AssistantMessage);
                if (!reply.HasToolCalls) return reply.AssistantMessage;
                await ExecuteBatch(turn, reply.AssistantMessage.ToolCalls, declarations, round, ct);
            }
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    public async IAsyncEnumerable<Message> RunTurnStreamAsync(string userText, ContextBuildOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new();
        Enter(userText, options, ct);
        try
        {
            var turn = StartTurn(userText);
            var declarations = _tools.GetDeclaredTools();
            for (var round = 0; ; round++)
            {
                ct.ThrowIfCancellationRequested();
                var accumulator = new StreamAccumulator();
                await foreach (var chunk in _llm.StreamCompleteAsync(Request(options, declarations), ct).WithCancellation(ct))
                {
                    ct.ThrowIfCancellationRequested();
                    if (chunk.Error != null) throw new InvalidOperationException($"LLM streaming error: {chunk.Error}");
                    if (chunk.Delta == null) continue;
                    accumulator.Add(chunk.Delta);
                    yield return chunk.Delta;
                }
                ct.ThrowIfCancellationRequested();
                var message = accumulator.Build();
                RecordAssistant(turn, message);
                if (message.ToolCalls.Count == 0) yield break;
                await ExecuteBatch(turn, message.ToolCalls, declarations, round, ct);
            }
        }
        finally { Volatile.Write(ref _running, 0); }
    }

    private void Enter(string userText, ContextBuildOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userText);
        options.Validate();
        ct.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            throw new InvalidOperationException("Concurrent turns on one orchestrator are not supported.");
    }

    private Turn StartTurn(string text)
    {
        var turn = new Turn { UserOrSystemMessage = new Message { Role = Role.User, Content = text } };
        _conversation.AddTurn(turn);
        return turn;
    }

    private LlmRequest Request(ContextBuildOptions options, IReadOnlyList<ToolDeclaration> declarations)
    {
        var messages = _contextManager.Build(options);
        if (messages.Count == 0)
            throw new InvalidOperationException("Context policy excluded the entire current turn. Increase the budget or message limit.");
        return new() { Messages = messages, Tools = declarations, Config = _execution.LlmConfig };
    }

    private void RecordAssistant(Turn turn, Message message)
    {
        if (turn.AssistantMessage == null) turn.AssistantMessage = message;
        else turn.ContinuationMessages.Add(message);
        _conversation.InvalidateCache();
    }

    private async Task ExecuteBatch(Turn turn, IReadOnlyList<ToolCall> calls,
        IReadOnlyList<ToolDeclaration> declarations, int round, CancellationToken ct)
    {
        // Produce matching error results before failing so history has no dangling calls.
        if (round >= _execution.MaxToolRounds)
        {
            foreach (var call in calls) RecordTool(turn, Error(call, "tool_round_limit"));
            throw new InvalidOperationException($"Tool round limit ({_execution.MaxToolRounds}) exceeded.");
        }
        foreach (var call in calls)
        {
            ct.ThrowIfCancellationRequested();
            var definition = declarations.FirstOrDefault(d => d.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase));
            ToolResult result;
            if (definition == null || !_tools.TryGet(call.Name, out var tool))
                result = Error(call, "tool_not_found");
            else
            {
                var validation = ToolCallValidator.Validate(call, definition);
                if (!validation.IsValid)
                    result = ToolResult.FromObject(call.Id, call.Name,
                        new { error = "validation_failed", details = validation.Errors }, isError: true);
                else
                {
                    try
                    {
                        result = await tool.ExecuteAsync(call, ct);
                        ct.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { result = Error(call, ex.Message); }
                }
            }
            RecordTool(turn, result);
        }
    }

    private static ToolResult Error(ToolCall call, string error)
        => ToolResult.FromObject(call.Id, call.Name, new { error }, isError: true);

    private void RecordTool(Turn turn, ToolResult result)
    {
        var message = new Message
        {
            Role = Role.Tool,
            Content = result.ResultJson,
            ToolResults = new() { result },
            Metadata = new()
            {
                ["tool_name"] = result.Name,
                ["tool_call_id"] = result.CallId,
                ["is_error"] = result.IsError,
                ["tool_not_found"] = result.ResultJson.Contains("\"tool_not_found\""),
                ["validation_error"] = result.ResultJson.Contains("\"validation_failed\"")
            }
        };
        if (turn.ContinuationMessages.Count == 0) turn.ToolMessages.Add(message);
        else turn.ContinuationMessages.Add(message);
        _conversation.InvalidateCache();
    }
}
