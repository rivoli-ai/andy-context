using System.Text.Json;
using Andy.Context.Model;

namespace Andy.Context.Utils;

#region Utilities

/// <summary>
/// Extension methods for conversation analysis.
/// </summary>
public static class ConversationExtensions
{
    /// <summary>
    /// Get conversation statistics.
    /// </summary>
    public static ConversationStats GetStats(this Conversation conversation)
    {
        var messages = conversation.GetCachedMessages();

        return new ConversationStats
        {
            TotalTurns = conversation.Turns.Count,
            TotalMessages = messages.Count,
            UserMessages = messages.Count(m => m.Role == Role.User),
            AssistantMessages = messages.Count(m => m.Role == Role.Assistant),
            ToolMessages = messages.Count(m => m.Role == Role.Tool),
            SystemMessages = messages.Count(m => m.Role == Role.System),
            ToolCalls = messages.Sum(m => m.ToolCalls.Count),
            ToolResults = messages.Sum(m => m.ToolResults.Count),
            ToolErrors = messages.Sum(m => m.ToolResults.Count(tr => tr.IsError)),
            FirstMessageAt = messages.FirstOrDefault()?.Timestamp,
            LastMessageAt = messages.LastOrDefault()?.Timestamp
        };
    }

    /// <summary>
    /// Get a summary of the conversation.
    /// </summary>
    public static string GetSummary(this Conversation conversation, int maxLength = 500)
    {
        var messages = conversation.GetCachedMessages();
        var summary = new System.Text.StringBuilder();

        foreach (var message in messages.TakeLast(10)) // Last 10 messages
        {
            var role = message.Role.ToString().ToLower();
            var content = message.Content.Length > 100
                ? message.Content.Substring(0, 100) + "..."
                : message.Content;

            summary.AppendLine($"{role}: {content}");
        }

        var result = summary.ToString();
        return result.Length > maxLength
            ? result.Substring(0, maxLength) + "..."
            : result;
    }

    /// <summary>
    /// Export conversation to JSON.
    /// </summary>
    public static string ToJson(this Conversation conversation)
    {
        return JsonSerializer.Serialize(conversation, JsonOptions.Default);
    }

    /// <summary>
    /// Import conversation from JSON.
    /// </summary>
    public static Conversation FromJson(string json)
    {
        return JsonSerializer.Deserialize<Conversation>(json, JsonOptions.Default)
               ?? throw new InvalidOperationException("Failed to deserialize conversation");
    }
}

#endregion