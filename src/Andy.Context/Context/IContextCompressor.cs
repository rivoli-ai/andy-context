using Andy.Context.Model;

namespace Andy.Context.Context;

/// <summary>
/// Interface for compressing a list of messages to fit a budget.
/// </summary>
public interface IContextCompressor
{
    List<Message> Compress(List<Message> messages, ContextBuildOptions options);
}