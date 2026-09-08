using Andy.Context.Model;

namespace Andy.Context.Context;

/// <summary>Drops oldest messages to fit limits, then removes any orphaned tool exchanges.</summary>
public sealed class SimpleCompressor : IContextCompressor
{
    /// <summary>Content-only legacy estimate; use TokenEstimator.Estimate for message budgets.</summary>
    public int EstimateTokens(string s) => Math.Max(1, s.Length / 4);

    public List<Message> Compress(List<Message> messages, ContextBuildOptions options)
        => ContextPolicy.Apply(messages, options, smart: false);
}
