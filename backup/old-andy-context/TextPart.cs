namespace Andy.Context;

/// <summary>
/// Text content part
/// </summary>
public class TextPart : MessagePart
{
    /// <summary>
    /// The text content
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <inheritdoc />
    public override int GetCharacterCount() => Text?.Length ?? 0;
}