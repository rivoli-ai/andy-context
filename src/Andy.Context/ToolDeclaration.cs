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
}


/// <summary>
/// Represents a function call from the LLM
/// </summary>
public class FunctionCall
{
    /// <summary>
    /// The function name to call
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Unique identifier for this function call
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Arguments to pass to the function
    /// </summary>
    public Dictionary<string, object?> Arguments { get; set; } = new();

    /// <summary>
    /// Raw JSON string for arguments when provider returns string; preserved for fidelity
    /// </summary>
    public string? ArgumentsJson { get; set; }
}
