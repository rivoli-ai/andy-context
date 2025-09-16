namespace Andy.Context.Tooling;

/// <summary>
/// Tool execution exception with structured error information.
/// </summary>
public class ToolExecutionException : Exception
{
    public string ToolName { get; }
    public string CallId { get; }
    public string ArgumentsJson { get; }
    
    public ToolExecutionException(string toolName, string callId, string argumentsJson, Exception innerException)
        : base($"Tool execution failed: {toolName}", innerException)
    {
        ToolName = toolName;
        CallId = callId;
        ArgumentsJson = argumentsJson;
    }
}