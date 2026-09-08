using Andy.Context.Model;

namespace Andy.Context.Context;

/// <summary>Drops oldest complete turns to fit limits without changing order or tool payloads.</summary>
public sealed class SmartCompressor : IContextCompressor
{
    public List<Message> Compress(List<Message> messages, ContextBuildOptions options)
        => ContextPolicy.Apply(messages, options, options.CompressionStrategy != CompressionStrategy.Simple);
}
