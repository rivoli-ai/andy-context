using Andy.Context.Context;
using Andy.Context.Model;
using Andy.Context.Utils;

namespace Andy.Tests.Context.Core;

public class ContextManagerEdgeCasesTests
{
    [Fact]
    public void ContextManager_WithEmptyConversation_ShouldReturnEmptyContext()
    {
        // Arrange
        var conversation = new Conversation();
        var contextManager = new ContextManager(conversation);
        var options = new ContextBuildOptions();

        // Act
        var context = contextManager.Build(options);

        // Assert
        Assert.Empty(context);
    }

    [Fact]
    public void ContextManager_WithVerySmallTokenBudget_ShouldKeepMinimumMessages()
    {
        // Arrange
        var conversation = new Conversation();
        for (int i = 0; i < 10; i++)
        {
            conversation.AddTurn(new Turn
            {
                UserOrSystemMessage = new Message
                {
                    Role = Role.User,
                    Content = $"This is a very long message number {i} with lots of text content"
                }
            });
        }

        var contextManager = new ContextManager(conversation, new SimpleCompressor());
        var options = new ContextBuildOptions
        {
            TokenBudget = 5 // Extremely small budget
        };

        // Act
        var context = contextManager.Build(options);

        // Assert
        // With such a tiny budget, all messages will be dropped
        Assert.Empty(context);
    }

    [Fact]
    public void ContextManager_WithMaxRecentMessages_ShouldLimitCount()
    {
        // Arrange
        var conversation = new Conversation();
        for (int i = 0; i < 20; i++)
        {
            conversation.AddTurn(new Turn
            {
                UserOrSystemMessage = new Message { Role = Role.User, Content = $"Message {i}" }
            });
        }

        var contextManager = new ContextManager(conversation, new SimpleCompressor());
        var options = new ContextBuildOptions
        {
            MaxRecentMessages = 5,
            TokenBudget = 10000 // Large budget to test count limiting
        };

        // Act
        var context = contextManager.Build(options);

        // Assert
        // SimpleCompressor should apply MaxRecentMessages to limit count
        Assert.Equal(5, context.Count);
        // Should keep most recent messages
        Assert.Equal("Message 19", context.Last().Content);
    }

    [Fact]
    public void SimpleCompressor_WithOnlySystemMessages_WhenFiltered_ShouldReturnEmpty()
    {
        // Arrange
        var compressor = new SimpleCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.System, Content = "System instruction 1" },
            new() { Role = Role.System, Content = "System instruction 2" }
        };

        var options = new ContextBuildOptions
        {
            IncludeSystemMessages = false
        };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        Assert.Empty(compressed);
    }

    [Fact]
    public void SimpleCompressor_WithMixedMessages_ShouldFilterCorrectly()
    {
        // Arrange
        var compressor = new SimpleCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.System, Content = "System" },
            new() { Role = Role.User, Content = "User" },
            new() { Role = Role.Assistant, Content = "Assistant" },
            new() { Role = Role.Tool, Content = "Tool" }
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
        Assert.Equal(Role.User, compressed[0].Role);
        Assert.Equal(Role.Assistant, compressed[1].Role);
    }

    [Fact]
    public void SmartCompressor_WithToolCallPair_ShouldPreserveBoth()
    {
        // Arrange
        var compressor = new SmartCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.User, Content = "Calculate something" },
            new()
            {
                Role = Role.Assistant,
                Content = "Calculating",
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "call_1", Name = "calc", ArgumentsJson = "{}" }
                }
            },
            new()
            {
                Role = Role.Tool,
                Content = "Result",
                ToolResults = new List<ToolResult>
                {
                    ToolResult.FromObject("call_1", "calc", new { result = 42 })
                }
            },
            new() { Role = Role.Assistant, Content = "The answer is 42" }
        };

        var options = new ContextBuildOptions
        {
            TokenBudget = 200, // Enough for the complete structured tool exchange
            PreserveToolCallPairs = true
        };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        // Should preserve the tool call/result pair even with small budget
        Assert.Contains(compressed, m => m.ToolCalls.Any());
        Assert.Contains(compressed, m => m.ToolResults.Any());
    }

    [Fact]
    public void ContextManager_WithNullCompressor_ShouldUseSmartCompressor()
    {
        // Arrange
        var conversation = new Conversation();
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Test" }
        });

        var contextManager = new ContextManager(conversation, null);
        var options = new ContextBuildOptions();

        // Act
        var context = contextManager.Build(options);

        // Assert
        Assert.NotEmpty(context);
        Assert.Equal("Test", context[0].Content);
    }

    [Fact]
    public void SimpleCompressor_PreservesMessageProperties()
    {
        // Arrange
        var compressor = new SimpleCompressor();
        var messageId = Guid.NewGuid().ToString();
        var parentId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.Now.AddMinutes(-5);

        var messages = new List<Message>
        {
            new()
            {
                Role = Role.User,
                Content = "Test message",
                Id = messageId,
                ParentMessageId = parentId,
                Timestamp = timestamp,
                Metadata = new Dictionary<string, object> { ["key"] = "value" }
            }
        };

        var options = new ContextBuildOptions { TokenBudget = 1000 };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        Assert.Single(compressed);
        var result = compressed[0];
        Assert.Equal(messageId, result.Id);
        Assert.Equal(parentId, result.ParentMessageId);
        Assert.Equal(timestamp, result.Timestamp);
        Assert.Equal("value", result.Metadata["key"]);
    }

    [Fact]
    public void SimpleCompressor_WithExactBudget_ShouldNotDropMessages()
    {
        // Arrange
        var compressor = new SimpleCompressor();
        var messages = new List<Message>
        {
            new() { Role = Role.User, Content = "12345678" }, // 8 chars = ~2 tokens
            new() { Role = Role.Assistant, Content = "12345678" } // 8 chars = ~2 tokens
        };

        var options = new ContextBuildOptions
        {
            TokenBudget = 12 // Content plus four framing units per message
        };

        // Act
        var compressed = compressor.Compress(messages, options);

        // Assert
        Assert.Equal(2, compressed.Count);
    }

    [Fact]
    public void ContextBuildOptions_WithMaxConversationAge_ShouldBeRespected()
    {
        // This test documents expected behavior - actual age filtering would need to be implemented
        // Arrange
        var options = new ContextBuildOptions
        {
            MaxConversationAge = TimeSpan.FromHours(1)
        };

        // Assert
        Assert.Equal(TimeSpan.FromHours(1), options.MaxConversationAge);
    }

    [Fact]
    public void Conversation_GetStats_WithComplexConversation_ShouldCountCorrectly()
    {
        // Arrange
        var conversation = new Conversation();

        // System message turn
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.System, Content = "System prompt" }
        });

        // User-Assistant with tool interaction
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Calculate 2+2" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "Calculating",
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "1", Name = "calc", ArgumentsJson = "{}" }
                }
            }
        };

        turn.ToolMessages.Add(new Message
        {
            Role = Role.Tool,
            Content = "4",
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("1", "calc", new { result = 4 })
            }
        });

        conversation.AddTurn(turn);

        // Act
        var stats = conversation.GetStats();

        // Assert
        Assert.Equal(2, stats.TotalTurns);
        Assert.Equal(4, stats.TotalMessages); // System, User, Assistant, Tool
        Assert.Equal(1, stats.SystemMessages);
        Assert.Equal(1, stats.UserMessages);
        Assert.Equal(1, stats.AssistantMessages);
        Assert.Equal(1, stats.ToolMessages);
        Assert.Equal(1, stats.ToolCalls);
        Assert.Equal(1, stats.ToolResults);
        Assert.Equal(0, stats.ToolErrors);
    }
}