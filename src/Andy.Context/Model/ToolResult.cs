using System.Text.Json;
using Andy.Context.Utils;

namespace Andy.Context.Model;

/// <summary>
/// Tool execution result added to conversation with Role=Tool.
/// </summary>
public sealed class ToolResult
{
    public string CallId { get; init; } = string.Empty; // must match ToolCall.Id
    public string Name { get; init; } = string.Empty;
    public bool IsError { get; init; }

    // Raw JSON result payload to keep model-agnostic fidelity.
    public string ResultJson { get; init; } = "{}";

    public static ToolResult FromObject(string callId, string name, object result, bool isError = false)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions.Default);
        return new ToolResult { CallId = callId, Name = name, ResultJson = json, IsError = isError };
    }
}