namespace Andy.Context;

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