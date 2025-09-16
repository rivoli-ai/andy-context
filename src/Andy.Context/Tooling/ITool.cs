using Andy.Context.Model;

namespace Andy.Context.Tooling;

/// <summary>
/// Implement to provide tool execution. Arguments arrive as JsonElement.
/// </summary>
public interface ITool
{
    ToolDeclaration Definition { get; }
    Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default);
}