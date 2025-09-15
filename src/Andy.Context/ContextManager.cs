using System.Text;

namespace Andy.Context;

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
        (
            MessageRole.Tool,
            // Per grounding policy: add RAW tool result content (prefer JSON) with proper call reference (capped)
            cappedResult,
            DateTime.UtcNow)
        {
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