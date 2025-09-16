namespace Andy.Context.Tooling;

/// <summary>
/// Retryable tool execution exception.
/// </summary>
public class RetryableToolException : ToolExecutionException
{
    public int RetryCount { get; }
    public TimeSpan RetryDelay { get; }
    
    public RetryableToolException(string toolName, string callId, string argumentsJson, Exception innerException, int retryCount = 0, TimeSpan retryDelay = default)
        : base(toolName, callId, argumentsJson, innerException)
    {
        RetryCount = retryCount;
        RetryDelay = retryDelay == default ? TimeSpan.FromSeconds(1) : retryDelay;
    }
}