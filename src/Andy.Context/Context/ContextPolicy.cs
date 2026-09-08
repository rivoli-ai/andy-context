using Andy.Context.Model;

namespace Andy.Context.Context;

internal static class ContextPolicy
{
    internal static List<Message> Apply(List<Message> messages, ContextBuildOptions options, bool smart)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        var now = DateTimeOffset.UtcNow;
        var filtered = messages.Where(m =>
            (options.IncludeSystemMessages || m.Role != Role.System) &&
            (options.IncludeToolMessages || m.Role != Role.Tool) &&
            now - m.Timestamp <= options.MaxConversationAge).ToList();
        if (options.PreserveToolCallPairs) RemoveOrphanedTools(filtered);
        if (options.CompressionStrategy == CompressionStrategy.None) return filtered;

        var groups = new List<List<Message>>();
        foreach (var message in filtered)
        {
            if (!smart || groups.Count == 0 || message.Role is Role.User or Role.System)
                groups.Add(new());
            groups[^1].Add(message);
        }
        long budget = filtered.Sum(m => (long)TokenEstimator.Estimate(m));
        var count = filtered.Count;
        var start = 0;
        while (start < groups.Count && (budget > options.TokenBudget || count > options.MaxRecentMessages))
        {
            budget -= groups[start].Sum(m => (long)TokenEstimator.Estimate(m));
            count -= groups[start++].Count;
        }
        var result = groups.Skip(start).SelectMany(g => g).ToList();
        if (options.PreserveToolCallPairs) RemoveOrphanedTools(result);
        return result;
    }

    // Remove both sides when a filter or tail cut drops part of an exchange.
    private static void RemoveOrphanedTools(List<Message> messages)
    {
        bool changed;
        do
        {
            var calls = new Dictionary<string, int>();
            var results = new Dictionary<string, int>();
            for (var i = 0; i < messages.Count; i++)
            {
                foreach (var call in messages[i].ToolCalls) calls.TryAdd(call.Id, i);
                foreach (var result in messages[i].ToolResults) results.TryAdd(result.CallId, i);
            }
            changed = messages.RemoveAll(m =>
                m.ToolCalls.Any(c => !results.TryGetValue(c.Id, out var ri) || ri <= calls[c.Id]) ||
                m.ToolResults.Any(r => !calls.TryGetValue(r.CallId, out var ci) || ci >= results[r.CallId])) > 0;
        } while (changed);
    }
}
