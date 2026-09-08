using System.Text;
using Andy.Context.Model;

namespace Andy.Context.Orchestration;

// Adapters send content/argument deltas, a stable call ID on every fragment,
// and the full tool name on its first fragment (or repeat that same name).
internal sealed class StreamAccumulator
{
    private Message? _first;
    private readonly StringBuilder _text = new();
    private readonly Dictionary<string, (string Name, StringBuilder Arguments)> _calls = new();

    internal void Add(Message delta)
    {
        _first ??= delta;
        _text.Append(delta.Content);
        foreach (var call in delta.ToolCalls)
        {
            if (string.IsNullOrWhiteSpace(call.Id)) throw new InvalidOperationException("Streamed tool calls require a stable ID.");
            if (!_calls.TryGetValue(call.Id, out var current))
                current = (call.Name, new StringBuilder());
            else if (!string.IsNullOrEmpty(call.Name) && current.Name != call.Name)
                throw new InvalidOperationException("Tool name changed within one streamed call.");
            current.Arguments.Append(call.ArgumentsJson);
            _calls[call.Id] = current;
        }
    }

    internal Message Build()
    {
        if (_first == null) throw new InvalidOperationException("LLM stream ended without a message.");
        return new()
        {
            Role = Role.Assistant,
            Content = _text.ToString(),
            Id = _first.Id,
            Timestamp = _first.Timestamp,
            ParentMessageId = _first.ParentMessageId,
            Metadata = _first.Metadata,
            ToolCalls = _calls.Select(c => new ToolCall
            {
                Id = c.Key,
                Name = c.Value.Name,
                ArgumentsJson = c.Value.Arguments.Length == 0 ? "{}" : c.Value.Arguments.ToString()
            }).ToList()
        };
    }
}
