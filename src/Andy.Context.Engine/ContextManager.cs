using Andy.Engine.Util;

namespace Andy.Engine.Context;

/// <summary>
/// Manages conversation context with compression and history
/// </summary>
public class ContextManager
{
    private readonly List<ContextEntry> _history = new();
    private readonly int _maxTokens;
    private readonly int _compressionThreshold;
    private string _systemPrompt;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="systemPrompt"></param>
    /// <param name="maxTokens"></param>
    /// <param name="compressionThreshold"></param>
    public ContextManager(
        string systemPrompt,
        int maxTokens = 12000,
        int compressionThreshold = 10000)
    {
        _systemPrompt = systemPrompt;
        _maxTokens = maxTokens;
        _compressionThreshold = compressionThreshold;
    }
    
    /// <summary>
    /// Add a user message to the context
    /// </summary>
    public void AddUserMessage(string message)
    {
        var userEntry = new ContextEntry(MessageRole.User, message, DateTime.UtcNow);
        _history.Add(userEntry);
    }

    /// <summary>
    /// Add an assistant message to the context
    /// </summary>
    public void AddAssistantMessage(string message, List<ContextToolCall>? toolCalls = null)
    {
        var userEntry = new ContextEntry(MessageRole.Assistant, message, DateTime.UtcNow)
        {
            ToolCalls = toolCalls
        };
        _history.Add(userEntry);
    }
    
    
    /// <summary>
    /// Add a tool execution to the context
    /// </summary>
    public void AddToolExecution(string toolId, string callId, Dictionary<string, object?> parameters, string result)
    {
        // Secondary cap for tool results (primary limiting happens in ToolOutputLimits)
        // This is a safety net in case tool output wasn't limited earlier
        const int maxToolResultChars = 3000;
        var cappedResult = result;
        if (!string.IsNullOrEmpty(result) && result.Length > maxToolResultChars)
        {
            // Include information about how much was truncated
            var totalChars = result.Length;
            var truncatedChars = totalChars - maxToolResultChars;
            // Use a cleaner truncation format that won't confuse JSON parsing
            cappedResult = result.Substring(0, maxToolResultChars) + 
                          $"\n\n... (output truncated - showing first {maxToolResultChars} of {totalChars} total characters)";
        }
        _history.Add(new ContextEntry
        {
            Role = MessageRole.Tool,
            // Per grounding policy: add RAW tool result content (prefer JSON) with proper call reference (capped)
            Content = cappedResult,
            Timestamp = DateTime.UtcNow,
            TokenEstimate = EstimateTokens(cappedResult),
            ToolId = toolId,
            ToolCallId = callId,
            ToolResult = cappedResult
        });
    }

    /// <summary>
    /// Get the message history
    /// </summary>
    public List<ContextEntry> GetHistory()
    {
        return new List<ContextEntry>(_history);
    }

    /// <summary>
    /// Get the current conversation context for LLM
    /// </summary>
    public ConversationContext GetContext()
    {
        var context = new ConversationContext
        {
            SystemInstruction = _systemPrompt
        };

        // Check if we need compression
        var totalTokens = EstimateTokens(_systemPrompt);
        foreach (var entry in _history)
        {
            totalTokens += entry.TokenEstimate;
        }

        if (totalTokens > _compressionThreshold)
        {
            // Compress older messages
            context = CompressContext();
        }
        else
        {
            // Add all messages
            foreach (var entry in _history)
            {
                if (entry.Role == MessageRole.User)
                {
                    context.AddUserMessage(entry.Content);
                }
                else if (entry.Role == MessageRole.Assistant)
                {
                    // If the assistant message includes tool calls, add them
                    if (entry.ToolCalls != null && entry.ToolCalls.Any())
                    {
                        // Convert to FunctionCall format
                        var functionCalls = entry.ToolCalls.Select(tc => new FunctionCall
                        {
                            Id = tc.CallId,
                            Name = tc.ToolId,
                            Arguments = tc.Parameters
                        }).ToList();

                        context.AddAssistantMessageWithToolCalls(entry.Content, functionCalls);
                    }
                    else
                    {
                        context.AddAssistantMessage(entry.Content);
                    }
                }
                else if (entry.Role == MessageRole.Tool)
                {
                    // Tool messages need to be added as tool responses
                    // They must reference a previous tool call
                    context.AddToolResponse(entry.ToolId ?? "tool", entry.ToolCallId ?? "call_" + Guid.NewGuid().ToString("N"), entry.Content);
                }
            }
        }

        return context;
    }

    /// <summary>
    /// Compress context when approaching token limit
    /// </summary>
    private ConversationContext CompressContext()
    {
        var context = new ConversationContext
        {
            SystemInstruction = _systemPrompt
        };

        // Keep the most recent messages and summarize older ones
        // But ensure we don't orphan tool responses from their tool calls
        var recentMessages = GetRecentMessagesWithoutOrphans(10);
        var olderMessages = _history.Take(_history.Count - recentMessages.Count).ToList();

        if (olderMessages.Any())
        {
            // Create a summary of older messages
            var summary = SummarizeMessages(olderMessages);
            context.AddAssistantMessage($"[Previous conversation summary: {summary}]");
        }

        // Add recent messages
        foreach (var entry in recentMessages)
        {
            if (entry.Role == MessageRole.User)
            {
                context.AddUserMessage(entry.Content);
            }
            else if (entry.Role == MessageRole.Assistant)
            {
                // If the assistant message includes tool calls, add them
                if (entry.ToolCalls != null && entry.ToolCalls.Any())
                {
                    // Convert to FunctionCall format
                    var functionCalls = entry.ToolCalls.Select(tc => new FunctionCall
                    {
                        Id = tc.CallId,
                        Name = tc.ToolId,
                        Arguments = tc.Parameters
                    }).ToList();

                    context.AddAssistantMessageWithToolCalls(entry.Content, functionCalls);
                }
                else
                {
                    context.AddAssistantMessage(entry.Content);
                }
            }
            else if (entry.Role == MessageRole.Tool)
            {
                // Use already-capped content to avoid overflowing provider limits
                context.AddToolResponse(
                    entry.ToolId ?? "tool",
                    entry.ToolCallId ?? "call_" + Guid.NewGuid().ToString("N"),
                    entry.Content
                );
            }
        }

        return context;
    }

    /// <summary>
    /// Get recent messages ensuring no orphaned tool responses
    /// </summary>
    private List<ContextEntry> GetRecentMessagesWithoutOrphans(int targetCount)
    {
        if (_history.Count <= targetCount)
            return _history.ToList();

        var result = new List<ContextEntry>();
        var toolCallIds = new HashSet<string>();
        
        // Start from the end and work backwards
        for (int i = _history.Count - 1; i >= 0 && result.Count < targetCount; i--)
        {
            var entry = _history[i];
            
            if (entry.Role == MessageRole.Tool)
            {
                // This is a tool response - we need to ensure we include its tool call
                var callId = entry.ToolCallId;
                if (!string.IsNullOrEmpty(callId))
                {
                    // Find the assistant message with this tool call
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var prevEntry = _history[j];
                        if (prevEntry.Role == MessageRole.Assistant && 
                            prevEntry.ToolCalls?.Any(tc => tc.CallId == callId) == true)
                        {
                            // We need to include this assistant message too
                            if (!result.Contains(prevEntry))
                            {
                                result.Insert(0, prevEntry);
                            }
                            break;
                        }
                    }
                }
                result.Insert(0, entry);
            }
            else if (entry.Role == MessageRole.Assistant && entry.ToolCalls?.Any() == true)
            {
                // This assistant message has tool calls - track them
                foreach (var tc in entry.ToolCalls)
                {
                    toolCallIds.Add(tc.CallId);
                }
                result.Insert(0, entry);
            }
            else
            {
                result.Insert(0, entry);
            }
        }
        
        // If we have too many messages due to including tool call pairs, take the most recent
        if (result.Count > targetCount * 1.5)
        {
            // Keep complete tool call/response pairs from the end
            var finalResult = new List<ContextEntry>();
            var seenCallIds = new HashSet<string>();
            
            for (int i = result.Count - 1; i >= 0 && finalResult.Count < targetCount; i--)
            {
                var entry = result[i];
                if (entry.Role == MessageRole.Tool)
                {
                    // Always include tool responses with their calls
                    finalResult.Insert(0, entry);
                    if (!string.IsNullOrEmpty(entry.ToolCallId))
                        seenCallIds.Add(entry.ToolCallId);
                }
                else if (entry.Role == MessageRole.Assistant && entry.ToolCalls?.Any() == true)
                {
                    // Include if any of its tool calls are in our seen set
                    if (entry.ToolCalls.Any(tc => seenCallIds.Contains(tc.CallId)))
                    {
                        finalResult.Insert(0, entry);
                    }
                    else if (finalResult.Count < targetCount)
                    {
                        finalResult.Insert(0, entry);
                    }
                }
                else if (finalResult.Count < targetCount)
                {
                    finalResult.Insert(0, entry);
                }
            }
            
            return finalResult;
        }
        
        return result;
    }

    /// <summary>
    /// Summarize a list of messages
    /// </summary>
    private string SummarizeMessages(List<ContextEntry> messages)
    {
        var summary = new StringBuilder();
        summary.AppendLine($"Discussed {messages.Count} messages:");

        // Group by topic/tool usage
        var toolCalls = messages.Where(m => m.Role == MessageRole.Tool).ToList();
        if (toolCalls.Any())
        {
            var toolGroups = toolCalls.GroupBy(t => t.ToolId);
            summary.AppendLine($"- Executed tools: {string.Join(", ", toolGroups.Select(g => $"{g.Key} ({g.Count()}x)"))}");
        }

        // Extract key topics (simplified - in production use NLP)
        var userMessages = messages.Where(m => m.Role == MessageRole.User).ToList();
        if (userMessages.Any())
        {
            summary.AppendLine($"- User asked about {userMessages.Count} topics");
        }

        return summary.ToString();
    }
    

    /// <summary>
    /// Estimate token count (rough approximation)
    /// </summary>
    private int EstimateTokens(string text)
    {
        // Rough estimate: 1 token ≈ 4 characters
        return text.Length / 4;
    }

    /// <summary>
    /// Clear the conversation history
    /// </summary>
    public void Clear()
    {
        _history.Clear();
    }

    /// <summary>
    /// Update the system prompt
    /// </summary>
    public void UpdateSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
    }

    /// <summary>
    /// Get conversation statistics
    /// </summary>
    public ContextStats GetStats()
    {
        return new ContextStats
        {
            MessageCount = _history.Count + 1, // +1 for system prompt
            EstimatedTokens = _history.Sum(h => h.TokenEstimate) + EstimateTokens(_systemPrompt),
            ToolCallCount = _history.Count(h => h.Role == MessageRole.Tool),
            OldestMessage = _history.FirstOrDefault()?.Timestamp,
            NewestMessage = _history.LastOrDefault()?.Timestamp
        };
    }
}


/// <summary>
/// Manages conversation context for LLM interactions
/// </summary>
public class ConversationContext
{
    private readonly List<Message> _messages = new();
    private readonly List<Message> _comprehensiveHistory = new();
    private string? _systemInstruction;

    /// <summary>
    /// Current conversation messages
    /// </summary>
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Complete conversation history including all interactions
    /// </summary>
    public IReadOnlyList<Message> ComprehensiveHistory => _comprehensiveHistory.AsReadOnly();

    /// <summary>
    /// System instruction/prompt
    /// </summary>
    public string? SystemInstruction
    {
        get => _systemInstruction;
        set
        {
            _systemInstruction = value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Update or add system message at the beginning
                var systemMessage = _messages.FirstOrDefault(m => m.Role == MessageRole.System);
                if (systemMessage != null)
                {
                    var textPart = systemMessage.Parts.OfType<TextPart>().FirstOrDefault();
                    if (textPart != null)
                    {
                        systemMessage.Parts[systemMessage.Parts.IndexOf(textPart)] = new TextPart { Text = value };
                    }
                }
                else
                {
                    _messages.Insert(0, Message.CreateText(MessageRole.System, value));
                }
            }
        }
    }

    /// <summary>
    /// Available tools for the conversation
    /// </summary>
    public List<ToolDeclaration> AvailableTools { get; set; } = new();

    /// <summary>
    /// Maximum number of messages to keep in context
    /// </summary>
    public int MaxContextMessages { get; set; } = 50;

    /// <summary>
    /// Maximum character count for context
    /// </summary>
    public int MaxContextCharacters { get; set; } = 100000;

    /// <summary>
    /// Adds a user message to the conversation
    /// </summary>
    public void AddUserMessage(string content)
    {
        var message = Message.CreateText(MessageRole.User, content);
        _messages.Add(message);
        _comprehensiveHistory.Add(message);
        TrimContext();
    }

    /// <summary>
    /// Adds an assistant message to the conversation
    /// </summary>
    public void AddAssistantMessage(string content)
    {
        var message = Message.CreateText(MessageRole.Assistant, content);
        _messages.Add(message);
        _comprehensiveHistory.Add(message);
        TrimContext();
    }

    /// <summary>
    /// Adds an assistant message with tool calls
    /// </summary>
    public void AddAssistantMessageWithToolCalls(string? content, List<FunctionCall> functionCalls)
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Parts = new List<MessagePart>()
        };

        // Add text content if present
        if (!string.IsNullOrEmpty(content))
        {
            message.Parts.Add(new TextPart { Text = content });
        }

        // Add tool calls
        foreach (var functionCall in functionCalls)
        {
            message.Parts.Add(new ToolCallPart
            {
                ToolName = functionCall.Name,
                CallId = functionCall.Id,
                Arguments = functionCall.Arguments
            });
        }

        _messages.Add(message);
        _comprehensiveHistory.Add(message);
        TrimContext();
    }

    /// <summary>
    /// Adds a tool response to the conversation
    /// </summary>
    public void AddToolResponse(string toolName, string callId, object response)
    {
        var message = new Message
        {
            Role = MessageRole.Tool,
            Parts = new List<MessagePart>
            {
                new ToolResponsePart
                {
                    ToolName = toolName,
                    CallId = callId,
                    Response = response
                }
            }
        };

        _messages.Add(message);
        _comprehensiveHistory.Add(message);
        TrimContext();
    }

    /// <summary>
    /// Gets the total character count of the current context
    /// </summary>
    public int GetCharacterCount()
    {
        return _messages.Sum(m => m.GetCharacterCount());
    }

    /// <summary>
    /// Clears the conversation context
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        _comprehensiveHistory.Clear();
        _systemInstruction = null;
    }

    /// <summary>
    /// Creates an LLM request from the current context
    /// </summary>
    public LlmRequest CreateRequest(string? model = null)
    {
        // Filter out system messages if SystemPrompt is being used
        var messagesToInclude = !string.IsNullOrEmpty(SystemInstruction)
            ? _messages.Where(m => m.Role != MessageRole.System).ToList()
            : new List<Message>(_messages);

        return new LlmRequest
        {
            Messages = messagesToInclude,
            Tools = AvailableTools.Any() ? AvailableTools : null,
            Model = model,
            SystemPrompt = SystemInstruction
        };
    }

    /// <summary>
    /// Trims the context to stay within limits
    /// </summary>
    private void TrimContext()
    {
        // Keep system message if present
        var systemMessage = _messages.FirstOrDefault(m => m.Role == MessageRole.System);
        var messagesToTrim = systemMessage != null
            ? _messages.Skip(1).ToList()
            : _messages.ToList();

        // Trim by message count
        while (_messages.Count > MaxContextMessages && messagesToTrim.Count > 2)
        {
            _messages.Remove(messagesToTrim[0]);
            messagesToTrim.RemoveAt(0);
        }

        // Trim by character count
        while (GetCharacterCount() > MaxContextCharacters && messagesToTrim.Count > 2)
        {
            _messages.Remove(messagesToTrim[0]);
            messagesToTrim.RemoveAt(0);
        }
    }

    /// <summary>
    /// Gets a summary of the conversation
    /// </summary>
    public string GetSummary()
    {
        var lines = new List<string>();

        foreach (var message in _messages)
        {
            var role = message.Role.ToString();
            var content = string.Join(" ", message.Parts.OfType<TextPart>().Select(p => p.Text));

            if (!string.IsNullOrEmpty(content))
            {
                lines.Add($"{role}: {content}");
            }
        }

        return string.Join("\n", lines);
    }
}
