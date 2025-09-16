using Andy.Context.Context;
using Andy.Context.Llm;
using Andy.Context.Model;
using Andy.Context.Tooling;

namespace Andy.Context.Orchestration;

#region Orchestration Engine

/// <summary>
/// Orchestrates: build context → call LLM → run tools → append results → (optionally) call LLM again.
/// </summary>
public sealed class AssistantOrchestrator
{
    private readonly Conversation _conversation;
    private readonly ContextManager _contextManager;
    private readonly ToolRegistry _tools;
    private readonly ILlmClient _llm;

    public AssistantOrchestrator(Conversation conversation, ToolRegistry tools, ILlmClient llm)
    {
        _conversation = conversation;
        _contextManager = new ContextManager(conversation);
        _tools = tools;
        _llm = llm;
    }

    public async Task<Message> RunTurnAsync(string userText, ContextBuildOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ContextBuildOptions();

        // 1) Record user message as a new turn.
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = userText }
        };
        _conversation.AddTurn(turn);

        // 2) Build context and call LLM.
        var ctx = _contextManager.Build(options);
        var decl = _tools.GetDeclaredTools();
        var first = await _llm.ChatAsync(ctx, decl, ct);
        turn.AssistantMessage = first.AssistantMessage;
        _conversation.InvalidateCache(); // Invalidate cache after adding assistant message

        // 3) If tool calls present, execute all and append results.
        if (first.HasToolCalls)
        {
            foreach (var call in first.AssistantMessage.ToolCalls)
            {
                // Validate tool call before execution
                var toolDef = decl.FirstOrDefault(t => t.Name.Equals(call.Name, StringComparison.OrdinalIgnoreCase));
                if (toolDef != null)
                {
                    var validation = ToolCallValidator.Validate(call, toolDef);
                    if (!validation.IsValid)
                    {
                        var validationError = ToolResult.FromObject(call.Id, call.Name, 
                            new { error = "validation_failed", details = validation.Errors }, isError: true);
                        var toolMsg = new Message
                        {
                            Role = Role.Tool,
                            Content = validationError.ResultJson,
                            ToolResults = new List<ToolResult> { validationError },
                            Metadata = new Dictionary<string, object> 
                            { 
                                ["tool_name"] = call.Name, 
                                ["tool_call_id"] = call.Id,
                                ["validation_error"] = true
                            }
                        };
                        turn.ToolMessages.Add(toolMsg);
                        continue;
                    }
                }

                if (_tools.TryGet(call.Name, out var tool))
                {
                    ToolResult result;
                    try
                    {
                        result = await tool.ExecuteAsync(call, ct);
                    }
                    catch (ToolExecutionException ex)
                    {
                        result = ToolResult.FromObject(call.Id, call.Name, 
                            new { error = ex.Message, tool_name = ex.ToolName, call_id = ex.CallId }, isError: true);
                    }
                    catch (Exception ex)
                    {
                        result = ToolResult.FromObject(call.Id, call.Name, 
                            new { error = ex.Message, type = ex.GetType().Name }, isError: true);
                    }
                    
                    var toolMsg = new Message
                    {
                        Role = Role.Tool,
                        Content = result.ResultJson, // LLMs typically expect raw JSON string here
                        ToolResults = new List<ToolResult> { result },
                        Metadata = new Dictionary<string, object> 
                        { 
                            ["tool_name"] = call.Name, 
                            ["tool_call_id"] = call.Id,
                            ["is_error"] = result.IsError
                        }
                    };
                    turn.ToolMessages.Add(toolMsg);
                }
                else
                {
                    var notFound = ToolResult.FromObject(call.Id, call.Name, 
                        new { error = "tool_not_found", available_tools = _tools.GetRegisteredToolNames() }, isError: true);
                    var toolMsg = new Message 
                    { 
                        Role = Role.Tool, 
                        Content = notFound.ResultJson, 
                        ToolResults = new List<ToolResult> { notFound },
                        Metadata = new Dictionary<string, object> 
                        { 
                            ["tool_name"] = call.Name, 
                            ["tool_call_id"] = call.Id,
                            ["tool_not_found"] = true
                        }
                    };
                    turn.ToolMessages.Add(toolMsg);
                }
            }

            // 4) After tools, rebuild context and get final assistant response.
            _conversation.InvalidateCache(); // Invalidate cache since we modified the turn
            var ctx2 = _contextManager.Build(options);
            var second = await _llm.ChatAsync(ctx2, decl, ct);
            // Update the assistant message content but preserve the tool calls from the first response
            turn.AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = second.AssistantMessage.Content,
                ToolCalls = first.AssistantMessage.ToolCalls, // Preserve the original tool calls
                Metadata = second.AssistantMessage.Metadata,
                Timestamp = second.AssistantMessage.Timestamp,
                Id = second.AssistantMessage.Id,
                ParentMessageId = second.AssistantMessage.ParentMessageId
            };
        }

        return turn.AssistantMessage!;
    }

    /// <summary>
    /// Run a streaming turn for real-time responses.
    /// </summary>
    public async IAsyncEnumerable<Message> RunTurnStreamAsync(string userText, ContextBuildOptions? options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new ContextBuildOptions();

        // 1) Record user message as a new turn.
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = userText }
        };
        _conversation.AddTurn(turn);

        // 2) Build context and stream LLM response.
        var ctx = _contextManager.Build(options);
        var decl = _tools.GetDeclaredTools();
        
        var hasToolCalls = false;
        var toolCalls = new List<ToolCall>();
        
        await foreach (var message in _llm.ChatStreamAsync(ctx, decl, ct))
        {
            if (message.ToolCalls.Any())
            {
                hasToolCalls = true;
                toolCalls.AddRange(message.ToolCalls);
            }
            
            yield return message;
        }

        // 3) If tool calls present, execute them and stream final response.
        if (hasToolCalls && toolCalls.Any())
        {
            // Execute tools (non-streaming for now)
            foreach (var call in toolCalls)
            {
                if (_tools.TryGet(call.Name, out var tool))
                {
                    ToolResult result;
                    try
                    {
                        result = await tool.ExecuteAsync(call, ct);
                    }
                    catch (Exception ex)
                    {
                        result = ToolResult.FromObject(call.Id, call.Name, 
                            new { error = ex.Message }, isError: true);
                    }
                    
                    var toolMsg = new Message
                    {
                        Role = Role.Tool,
                        Content = result.ResultJson,
                        ToolResults = new List<ToolResult> { result },
                        Metadata = new Dictionary<string, object> 
                        { 
                            ["tool_name"] = call.Name, 
                            ["tool_call_id"] = call.Id
                        }
                    };
                    turn.ToolMessages.Add(toolMsg);
                }
            }

            // Stream final response after tool execution
            var ctx2 = _contextManager.Build(options);
            await foreach (var message in _llm.ChatStreamAsync(ctx2, decl, ct))
            {
                yield return message;
            }
        }
    }
}

#endregion