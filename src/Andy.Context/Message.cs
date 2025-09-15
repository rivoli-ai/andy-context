namespace Andy.Context;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public class Message
{
    /// <summary>
    /// The role of the message sender
    /// </summary>
    public MessageRole Role { get; set; }

    /// <summary>
    /// The message parts (text, tool calls, etc.)
    /// </summary>
    public List<MessagePart> Parts { get; set; } = new();

    /// <summary>
    /// Helper to create a simple text message
    /// </summary>
    public static Message CreateText(MessageRole role, string content)
    {
        return new Message
        {
            Role = role,
            Parts = new List<MessagePart> { new TextPart { Text = content } }
        };
    }

    /// <summary>
    /// Gets the total character count of the message
    /// </summary>
    public int GetCharacterCount()
    {
        return Parts.Sum(p => p.GetCharacterCount());
    }
}