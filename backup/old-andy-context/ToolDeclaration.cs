namespace Andy.Context;

/// <summary>
/// Declares a tool/function available to the LLM
/// </summary>
public class ToolDeclaration
{
    /// <summary>
    /// The name of the tool
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of what the tool does
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// JSON Schema of the tool's parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Whether this tool is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Convert this tool declaration to OpenAI function calling format
    /// </summary>
    public object ToOpenAIFunctionFormat()
    {
        return new
        {
            type = "function",
            function = new
            {
                name = Name,
                description = Description,
                parameters = Parameters
            }
        };
    }

    /// <summary>
    /// Convert this tool declaration to Anthropic tool format
    /// </summary>
    public object ToAnthropicToolFormat()
    {
        return new
        {
            name = Name,
            description = Description,
            input_schema = Parameters
        };
    }
}