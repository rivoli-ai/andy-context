namespace Andy.Context;

/// <summary>
/// Tool response part
/// </summary>
public class ToolResponsePart : MessagePart
{
    /// <summary>
    /// Name of the tool that was called
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// The call ID this is responding to
    /// </summary>
    public string CallId { get; set; } = string.Empty;

    /// <summary>
    /// The response from the tool
    /// </summary>
    public object? Response { get; set; }

    /// <inheritdoc />
    public override int GetCharacterCount()
    {
        var responseStr = Response?.ToString() ?? string.Empty;
        return ToolName.Length + CallId.Length + responseStr.Length;
    }
}