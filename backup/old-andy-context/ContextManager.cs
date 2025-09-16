using System.Text;

namespace Andy.Context;

/// <summary>
/// Context manager that clearly represents AI assistant ↔ LLM conversation flow
/// </summary>
public class ContextManager
{
    private readonly List<Message> _history = new();
    private readonly int _maxTokens;
    private readonly int _compressionThreshold;
    private string _systemPrompt;
    private readonly List<ToolDeclaration> _availableTools = new();

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
    /// Add a user message to the conversation
    /// </summary>
    public void AddUserMessage(string message)
    {
        var messageObj = Message.CreateText(MessageRole.User, message);
        _history.Add(messageObj);
    }

    /// <summary>
    /// Add an assistant message to the conversation
    /// </summary>
    public void AddAssistantMessage(string message)
    {
        var messageObj = Message.CreateText(MessageRole.Assistant, message);
        _history.Add(messageObj);
    }

    /// <summary>
    /// Add a tool execution result to the conversation
    /// </summary>
    public void AddToolExecution(string toolName, string callId, Dictionary<string, object?> parameters, string result)
    {
        // Cap tool results to prevent context overflow
        const int maxToolResultChars = 3000;
        var cappedResult = result;
        
        if (result.Length > maxToolResultChars)
        {
            var totalChars = result.Length;
            cappedResult = result.Substring(0, maxToolResultChars) +
                          $"\n\n... (output truncated - showing first {maxToolResultChars} of {totalChars} total characters)";
        }

        var messageObj = Message.CreateText(MessageRole.Tool, cappedResult);
        _history.Add(messageObj);
    }

    /// <summary>
    /// Get the conversation history
    /// </summary>
    public List<Message> GetHistory()
    {
        return new List<Message>(_history);
    }


    /// <summary>
    /// Get the current conversation context for LLM
    /// </summary>
    public ConversationContext GetContext()
    {
        var context = new ConversationContext
        {
            SystemInstruction = _systemPrompt,
            AvailableTools = new List<ToolDeclaration>(_availableTools)
        };

        // Add all messages
        foreach (var message in _history)
        {
            if (message.Role == MessageRole.User)
            {
                var textPart = message.Parts.OfType<TextPart>().FirstOrDefault();
                if (textPart != null)
                {
                    context.AddUserMessage(textPart.Text);
                }
            }
            else if (message.Role == MessageRole.Assistant)
            {
                var textPart = message.Parts.OfType<TextPart>().FirstOrDefault();
                if (textPart != null)
                {
                    context.AddAssistantMessage(textPart.Text);
                }
            }
            else if (message.Role == MessageRole.Tool)
            {
                var textPart = message.Parts.OfType<TextPart>().FirstOrDefault();
                if (textPart != null)
                {
                    context.AddToolResponse("tool", "call_1", textPart.Text);
                }
            }
        }

        return context;
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
    /// Add a tool declaration to the available tools
    /// </summary>
    public void AddTool(ToolDeclaration tool)
    {
        _availableTools.Add(tool);
    }

    /// <summary>
    /// Get all available tools
    /// </summary>
    public List<ToolDeclaration> GetAvailableTools()
    {
        return new List<ToolDeclaration>(_availableTools);
    }

    /// <summary>
    /// Get conversation statistics
    /// </summary>
    public ContextStats GetStats()
    {
        return new ContextStats
        {
            MessageCount = _history.Count + 1, // +1 for system prompt
            EstimatedTokens = _history.Sum(h => h.GetCharacterCount()) + EstimateTokens(_systemPrompt),
            ToolCallCount = _history.Count(h => h.Role == MessageRole.Tool),
            OldestMessage = null, // Simplified - no timestamp tracking
            NewestMessage = null
        };
    }
}