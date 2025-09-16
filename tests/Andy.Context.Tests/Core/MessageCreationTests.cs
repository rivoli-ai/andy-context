using System.Text.Json;
using Andy.Context.Model;
using Andy.Context.Utils;

namespace Andy.Tests.Context.Core;

public class MessageCreationTests
{
    [Fact]
    public void Message_ShouldInitializeWithDefaults()
    {
        // Arrange & Act
        var message = new Message();

        // Assert
        Assert.Equal(Role.System, message.Role); // Default role is System
        Assert.Equal(string.Empty, message.Content);
        Assert.Empty(message.ToolCalls);
        Assert.Empty(message.ToolResults);
        Assert.Empty(message.Metadata);
        Assert.NotEqual(default(DateTimeOffset), message.Timestamp);
        Assert.NotNull(message.Id);
    }

    [Fact]
    public void Message_WithUserContent_ShouldCreateCorrectly()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = Role.User,
            Content = "Hello, AI assistant!"
        };

        // Assert
        Assert.Equal(Role.User, message.Role);
        Assert.Equal("Hello, AI assistant!", message.Content);
        Assert.Empty(message.ToolCalls);
        Assert.Empty(message.ToolResults);
    }

    [Fact]
    public void Message_WithSystemInstruction_ShouldCreateCorrectly()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = Role.System,
            Content = "You are a helpful assistant"
        };

        // Assert
        Assert.Equal(Role.System, message.Role);
        Assert.Equal("You are a helpful assistant", message.Content);
    }

    [Fact]
    public void Message_WithToolCalls_ShouldCreateComplexMessage()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "I'll check the weather for you",
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_123",
                    Name = "get_weather",
                    ArgumentsJson = JsonSerializer.Serialize(new { location = "NYC", units = "fahrenheit" })
                },
                new ToolCall
                {
                    Id = "call_456",
                    Name = "get_time",
                    ArgumentsJson = JsonSerializer.Serialize(new { timezone = "EST" })
                }
            }
        };

        // Assert
        Assert.Equal(Role.Assistant, message.Role);
        Assert.Equal("I'll check the weather for you", message.Content);
        Assert.Equal(2, message.ToolCalls.Count);

        var firstCall = message.ToolCalls[0];
        Assert.Equal("call_123", firstCall.Id);
        Assert.Equal("get_weather", firstCall.Name);
        Assert.Contains("NYC", firstCall.ArgumentsJson);

        var secondCall = message.ToolCalls[1];
        Assert.Equal("call_456", secondCall.Id);
        Assert.Equal("get_time", secondCall.Name);
        Assert.Contains("EST", secondCall.ArgumentsJson);
    }

    [Fact]
    public void Message_WithToolResults_ShouldCreateToolMessage()
    {
        // Arrange
        var weatherResult = new { temperature = 72, condition = "sunny" };
        var timeResult = new { time = "2:30 PM", timezone = "EST" };

        // Act
        var message = new Message
        {
            Role = Role.Tool,
            Content = JsonSerializer.Serialize(new { weather = weatherResult, time = timeResult }),
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("call_123", "get_weather", weatherResult),
                ToolResult.FromObject("call_456", "get_time", timeResult)
            }
        };

        // Assert
        Assert.Equal(Role.Tool, message.Role);
        Assert.Equal(2, message.ToolResults.Count);

        var firstResult = message.ToolResults[0];
        Assert.Equal("call_123", firstResult.CallId);
        Assert.Equal("get_weather", firstResult.Name);
        Assert.False(firstResult.IsError);
        Assert.Contains("temperature", firstResult.ResultJson);

        var secondResult = message.ToolResults[1];
        Assert.Equal("call_456", secondResult.CallId);
        Assert.Equal("get_time", secondResult.Name);
        Assert.Contains("2:30 PM", secondResult.ResultJson);
    }

    [Fact]
    public void Message_WithErrorToolResult_ShouldMarkAsError()
    {
        // Arrange
        var errorResult = new { error = "API rate limit exceeded", code = 429 };

        // Act
        var message = new Message
        {
            Role = Role.Tool,
            Content = "Error occurred",
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("call_789", "get_weather", errorResult, isError: true)
            }
        };

        // Assert
        Assert.Equal(Role.Tool, message.Role);
        Assert.Single(message.ToolResults);

        var result = message.ToolResults[0];
        Assert.True(result.IsError);
        Assert.Equal("call_789", result.CallId);
        Assert.Contains("rate limit", result.ResultJson);
    }

    [Fact]
    public void Message_WithMetadata_ShouldStoreAdditionalData()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "Response",
            Metadata = new Dictionary<string, object>
            {
                ["model"] = "gpt-4",
                ["tokens"] = 150,
                ["latency_ms"] = 523.7,
                ["cached"] = true
            }
        };

        // Assert
        Assert.Equal(4, message.Metadata.Count);
        Assert.Equal("gpt-4", message.Metadata["model"]);
        Assert.Equal(150, message.Metadata["tokens"]);
        Assert.Equal(523.7, message.Metadata["latency_ms"]);
        Assert.Equal(true, message.Metadata["cached"]);
    }

    [Fact]
    public void Message_ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var message = new Message
        {
            Role = Role.User,
            Content = "What's the weather?"
        };

        // Act
        var result = message.ToString();

        // Assert
        Assert.Equal("[User] What's the weather?", result);
    }

    [Fact]
    public void Message_WithParentMessageId_ShouldLinkMessages()
    {
        // Arrange
        var parentId = Guid.NewGuid().ToString();

        // Act
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "Follow-up response",
            ParentMessageId = parentId
        };

        // Assert
        Assert.Equal(parentId, message.ParentMessageId);
    }

    [Fact]
    public void Turn_WithCompleteInteraction_ShouldEnumerateAllMessages()
    {
        // Arrange
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Calculate 5+3" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "I'll calculate that",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall { Id = "calc_1", Name = "calculator", ArgumentsJson = "{\"expression\":\"5+3\"}" }
                }
            },
            ToolMessages = new List<Message>
            {
                new Message
                {
                    Role = Role.Tool,
                    Content = "8",
                    ToolResults = new List<ToolResult>
                    {
                        ToolResult.FromObject("calc_1", "calculator", new { result = 8 })
                    }
                }
            }
        };

        // Act
        var messages = turn.EnumerateMessages().ToList();

        // Assert
        Assert.Equal(3, messages.Count);
        Assert.Equal(Role.User, messages[0].Role);
        Assert.Equal(Role.Assistant, messages[1].Role);
        Assert.Equal(Role.Tool, messages[2].Role);
        Assert.Single(messages[1].ToolCalls);
        Assert.Single(messages[2].ToolResults);
    }
}