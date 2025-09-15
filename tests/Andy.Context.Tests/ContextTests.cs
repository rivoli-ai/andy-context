using Andy.Context;

namespace Andy.Tests.Context;

public class ContextTests
{
    [Fact]
    public void CanAddUserEntry()
    {
        var systemPrompt = "You are a helpful assistant.";
        var sut = new ContextManager(systemPrompt);
        
        var msg = "Hello, this is a user message.";
        
        
        sut.AddUserMessage(msg);
        
        var history = sut.GetHistory();
        Assert.Single(history);
        var userEntry = history[0];
        
        Assert.Equal(MessageRole.User, userEntry.Role);
        
    }
}