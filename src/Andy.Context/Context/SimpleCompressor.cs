using Andy.Context.Model;

namespace Andy.Context.Context;

/// <summary>
/// Simple heuristic compressor. No external model: truncates older messages
/// and shortens long contents while preserving structure and the most recent turns.
/// </summary>
public sealed class SimpleCompressor : IContextCompressor
{
    public int EstimateTokens(string s) => Math.Max(1, s.Length / 4); // crude heuristic

    public List<Message> Compress(List<Message> messages, ContextBuildOptions options)
    {
        // Keep most recent messages first; drop earliest until under budget.
        var ordered = messages.ToList();
        var filtered = new List<Message>();

        // Optionally filter roles.
        foreach (var m in ordered)
        {
            if (!options.IncludeSystemMessages && m.Role == Role.System) continue;
            if (!options.IncludeToolMessages && m.Role == Role.Tool) continue;
            filtered.Add(m);
        }

        // Keep only recent tail if too many messages.
        if (filtered.Count > options.MaxRecentMessages)
            filtered = filtered.Skip(filtered.Count - options.MaxRecentMessages).ToList();

        // Enforce token budget by dropping older messages entirely.
        long total = filtered.Sum(m => (long)TokenEstimator.Estimate(m));
        while (total > options.TokenBudget && filtered.Count > 0)
        {
            // Remove the oldest message
            filtered.RemoveAt(0);
            total = filtered.Sum(m => (long)TokenEstimator.Estimate(m));
        }
        return filtered;
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        return s.Substring(0, Math.Max(0, max - 3)) + "...";
    }
}