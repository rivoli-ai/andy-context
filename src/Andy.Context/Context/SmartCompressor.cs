using Andy.Context.Model;

namespace Andy.Context.Context;

/// <summary>Drops oldest complete turns to fit the budget without changing order or tool payloads.</summary>
public sealed class SmartCompressor : IContextCompressor
{
    public List<Message> Compress(List<Message> messages, ContextBuildOptions options)
    {
        if (options.CompressionStrategy == CompressionStrategy.Simple)
            return new SimpleCompressor().Compress(messages, options);

        var turns = new List<List<Message>>();
        foreach (var message in messages)
        {
            if (turns.Count == 0 || message.Role is Role.User or Role.System)
                turns.Add(new List<Message>());
            turns[^1].Add(message);
        }
        long total = turns.SelectMany(t => t).Sum(m => (long)TokenEstimator.Estimate(m));
        var start = 0;
        while (total > options.TokenBudget && start < turns.Count)
            total -= turns[start++].Sum(m => (long)TokenEstimator.Estimate(m));
        return turns.Skip(start).SelectMany(t => t).ToList();
    }
}
