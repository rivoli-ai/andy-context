using Andy.Context.Model;

namespace Andy.Context.Context;

#region Context Management & Compression

/// <summary>
/// Builds the next context for the LLM from conversation history under a policy.
/// </summary>
public sealed class ContextManager
{
    private readonly Conversation _conversation;
    private readonly IContextCompressor _compressor;

    public ContextManager(Conversation conversation, IContextCompressor? compressor = null)
    {
        _conversation = conversation;
        _compressor = compressor ?? new SmartCompressor();
    }

    public List<Message> Build(ContextBuildOptions options)
    {
        var all = _conversation.GetCachedMessages();
        return _compressor.Compress(all, options);
    }
}

#endregion