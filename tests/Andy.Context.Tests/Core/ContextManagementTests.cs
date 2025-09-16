using System.Linq;
using Andy.Context.Context;
using Andy.Context.Model;

namespace Andy.Tests.Context.Core;

public class ContextManagementTests
{
    [Fact]
    public void SimpleCompressor_Compress_TruncatesCorrectly()
    {
        // Arrange
        var compressor = new SimpleCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.User, Content = "This is a very long message that should be truncated when compressed" }, //68 chars
            new() { Role = Role.Assistant, Content = "Short response" }, // 14 chars
            new() { Role = Role.User, Content = "Another long message that exceeds the token budget" } // 50 chars 
        };
        
        var options = new ContextBuildOptions
        {
            TokenBudget = 15, // Very small budget -- 132 / 4 = 33 tokens approx for all messages 
            MaxRecentMessages = 10
        };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        Assert.Equal(2, compressed.Count);
        Assert.Equal(compressed[0], messages[1]);
        Assert.Equal(compressed[1], messages[2]);
    }

    [Fact]
    public void SimpleCompressor_Compress_RespectsRoleFilters()
    {
        // Arrange
        var compressor = new SimpleCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.System, Content = "System message" },
            new() { Role = Role.User, Content = "User message" },
            new() { Role = Role.Tool, Content = "Tool message" },
            new() { Role = Role.Assistant, Content = "Assistant message" }
        };
        
        var options = new ContextBuildOptions
        {
            IncludeSystemMessages = false,
            IncludeToolMessages = false
        };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        Assert.Equal(2, compressed.Count);
        Assert.DoesNotContain(compressed, m => m.Role == Role.System);
        Assert.DoesNotContain(compressed, m => m.Role == Role.Tool);
    }

    [Fact]
    public void SmartCompressor_Compress_PreservesToolCallPairs()
    {
        // Arrange
        var compressor = new SmartCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.User, Content = "Calculate 2+2" },
            new() { Role = Role.Assistant, Content = "I'll calculate that", ToolCalls = new List<ToolCall> { new() { Id = "call_1", Name = "calculator" } } },
            new() { Role = Role.Tool, Content = "4", ToolResults = new List<ToolResult> { new() { CallId = "call_1", Name = "calculator" } } },
            new() { Role = Role.Assistant, Content = "The result is 4" }
        };
        
        var options = new ContextBuildOptions
        {
            TokenBudget = 100,
            PreserveToolCallPairs = true
        };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        // Should preserve the tool call/result pair
        Assert.True(compressed.Any(m => m.ToolCalls.Any()));
        Assert.True(compressed.Any(m => m.ToolResults.Any()));
    }

    [Fact]
    public void ContextManager_Build_ReturnsCompressedContext()
    {
        // Arrange
        var conversation = new Conversation();
        for (int i = 0; i < 10; i++)
        {
            conversation.AddTurn(new Turn
            {
                UserOrSystemMessage = new Message { Role = Role.User, Content = $"Message {i}" }
            });
        }

        var contextManager = new ContextManager(conversation, new SimpleCompressor());
        var options = new ContextBuildOptions
        {
            MaxRecentMessages = 3
        };

        // Act
        var context = contextManager.Build(options);

        // Assert
        Assert.True(context.Count <= 3);
        Assert.True(context.Count > 0);
    }

    [Fact]
    public void ContextBuildOptions_DefaultValues_AreReasonable()
    {
        // Act
        var options = new ContextBuildOptions();

        // Assert
        Assert.Equal(4000, options.TokenBudget);
        Assert.Equal(20, options.MaxRecentMessages);
        Assert.True(options.IncludeToolMessages);
        Assert.True(options.IncludeSystemMessages);
        Assert.Equal(TimeSpan.FromHours(24), options.MaxConversationAge);
        Assert.True(options.PreserveToolCallPairs);
        Assert.Equal(CompressionStrategy.Smart, options.CompressionStrategy);
    }
}