using System.Text.Json;

namespace Andy.Context.Model;

/// <summary>
/// Tool call emitted by an LLM in an assistant message.
/// </summary>
public sealed class ToolCall
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; init; } = string.Empty;

    // Raw JSON arguments; keeps fidelity across models.
    public string ArgumentsJson { get; init; } = "{}";

    public JsonElement ArgumentsAsJsonElement()
    {
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(ArgumentsJson) ? "{}" : ArgumentsJson).RootElement.Clone();
    }
}