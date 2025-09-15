namespace Andy.Context;

/// <summary>
/// Tool call part from assistant
/// </summary>
public class ToolCallPart : MessagePart
{
    /// <summary>
    /// Name of the tool to call
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for this call
    /// </summary>
    public string CallId { get; set; } = string.Empty;

    /// <summary>
    /// Arguments to pass to the tool
    /// </summary>
    public Dictionary<string, object?> Arguments { get; set; } = new();

    /// <inheritdoc />
    public override int GetCharacterCount()
    {
        var argJson = System.Text.Json.JsonSerializer.Serialize(Arguments);
        return ToolName.Length + CallId.Length + argJson.Length;
    }
}