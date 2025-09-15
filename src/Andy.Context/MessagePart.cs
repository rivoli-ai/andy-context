namespace Andy.Context;

/// <summary>
/// Base class for message parts
/// </summary>
public abstract class MessagePart
{
    /// <summary>
    /// Gets the character count of this part
    /// </summary>
    public abstract int GetCharacterCount();
}