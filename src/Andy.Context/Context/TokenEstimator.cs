using System.Text;
using System.Text.Json;
using Andy.Context.Model;
using Andy.Context.Utils;

namespace Andy.Context.Context;

/// <summary>
/// Model-independent budget units: UTF-8 bytes divided by four, rounded up,
/// plus four framing units per message. Includes structured tool payloads.
/// This is a heuristic, not a provider tokenizer.
/// </summary>
public static class TokenEstimator
{
    public static int Estimate(Message message)
    {
        long bytes = Encoding.UTF8.GetByteCount(message.Content);
        if (message.ToolCalls.Count > 0)
            bytes += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(message.ToolCalls, JsonOptions.Default));
        if (message.ToolResults.Count > 0)
            bytes += Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(message.ToolResults, JsonOptions.Default));
        return (int)Math.Min(int.MaxValue, 4 + (bytes + 3) / 4);
    }
}
