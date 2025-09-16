using Andy.Context.Model;

namespace Andy.Context.Context;

/// <summary>
/// Smart compressor that preserves tool call/result pairs and uses better strategies.
/// </summary>
public sealed class SmartCompressor : IContextCompressor
{
    private readonly SimpleCompressor _simpleCompressor = new();

    public List<Message> Compress(List<Message> messages, ContextBuildOptions options)
    {
        if (options.CompressionStrategy == CompressionStrategy.Simple)
        {
            return _simpleCompressor.Compress(messages, options);
        }

        // Smart compression logic
        var result = new List<Message>();
        var toolCallPairs = new Dictionary<string, List<Message>>();
        
        // Group messages by turns and preserve tool call/result pairs
        var turns = GroupIntoTurns(messages);
        
        // Keep recent turns intact, compress older ones
        var recentTurns = turns.TakeLast(options.MaxRecentMessages / 2).ToList();
        var olderTurns = turns.SkipLast(options.MaxRecentMessages / 2).ToList();
        
        // Add recent turns
        foreach (var turn in recentTurns)
        {
            result.AddRange(turn);
        }
        
        // Compress older turns
        foreach (var turn in olderTurns)
        {
            var compressedTurn = CompressTurn(turn, options);
            result.AddRange(compressedTurn);
        }
        
        // Apply token budget
        return ApplyTokenBudget(result, options);
    }

    private List<List<Message>> GroupIntoTurns(List<Message> messages)
    {
        var turns = new List<List<Message>>();
        var currentTurn = new List<Message>();
        
        foreach (var message in messages)
        {
            if (message.Role == Role.User || message.Role == Role.System)
            {
                if (currentTurn.Any())
                {
                    turns.Add(currentTurn);
                    currentTurn = new List<Message>();
                }
            }
            currentTurn.Add(message);
        }
        
        if (currentTurn.Any())
        {
            turns.Add(currentTurn);
        }
        
        return turns;
    }

    private List<Message> CompressTurn(List<Message> turn, ContextBuildOptions options)
    {
        if (!options.PreserveToolCallPairs)
        {
            return _simpleCompressor.Compress(turn, options);
        }
        
        // Preserve tool call/result pairs
        var result = new List<Message>();
        var toolCallIds = new HashSet<string>();
        
        // Find all tool calls in this turn
        foreach (var message in turn)
        {
            foreach (var toolCall in message.ToolCalls)
            {
                toolCallIds.Add(toolCall.Id);
            }
        }
        
        // Keep messages that contain tool calls or their results
        foreach (var message in turn)
        {
            var shouldKeep = false;
            
            // Keep if it's a user/system message
            if (message.Role == Role.User || message.Role == Role.System)
            {
                shouldKeep = true;
            }
            // Keep if it contains tool calls
            else if (message.ToolCalls.Any())
            {
                shouldKeep = true;
            }
            // Keep if it's a tool result for a call in this turn
            else if (message.Role == Role.Tool && message.ToolResults.Any(tr => toolCallIds.Contains(tr.CallId)))
            {
                shouldKeep = true;
            }
            // Keep assistant messages that reference tool results
            else if (message.Role == Role.Assistant && message.Content.Contains("tool"))
            {
                shouldKeep = true;
            }
            
            if (shouldKeep)
            {
                result.Add(message);
            }
        }
        
        return result;
    }

    private List<Message> ApplyTokenBudget(List<Message> messages, ContextBuildOptions options)
    {
        var totalTokens = messages.Sum(m => EstimateTokens(m.Content));
        
        if (totalTokens <= options.TokenBudget)
        {
            return messages;
        }
        
        // Trim from the beginning while preserving structure
        var result = new List<Message>(messages);
        var i = 0;
        
        while (totalTokens > options.TokenBudget && i < result.Count)
        {
            var message = result[i];
            var originalTokens = EstimateTokens(message.Content);
            
            // Don't truncate system messages
            if (message.Role == Role.System)
            {
                i++;
                continue;
            }
            
            // Truncate older messages more aggressively
            var maxLength = i < result.Count / 2 ? 100 : 200;
            var truncatedContent = Truncate(message.Content, maxLength);
            
            result[i] = new Message
            {
                Role = message.Role,
                Content = truncatedContent,
                Metadata = message.Metadata,
                Timestamp = message.Timestamp,
                Id = message.Id,
                ParentMessageId = message.ParentMessageId,
                ToolCalls = message.ToolCalls,
                ToolResults = message.ToolResults
            };
            
            totalTokens = totalTokens - originalTokens + EstimateTokens(truncatedContent);
            i++;
        }
        
        return result;
    }

    private int EstimateTokens(string s) => Math.Max(1, s.Length / 4);

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, Math.Max(0, max - 3)) + "...";
    }
}