using System.Text.Json;
using Andy.Context.Model;

namespace Andy.Tests.Context.Core;

public class MessageModelTests
{
    [Fact]
    public void ChatMessage_Constructor_InitializesCorrectly()
    {
        // Act
        var message = new Message
        {
            Role = Role.User,
            Content = "Hello, world!"
        };

        // Assert
        Assert.Equal(Role.User, message.Role);
        Assert.Equal("Hello, world!", message.Content);
        Assert.Empty(message.ToolCalls);
        Assert.Empty(message.ToolResults);
        Assert.Empty(message.Metadata);
        Assert.NotEmpty(message.Id);
        Assert.True(message.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ChatMessage_WithToolCalls_WorksCorrectly()
    {
        // Arrange
        var toolCall = new ToolCall
        {
            Name = "search",
            ArgumentsJson = """{"query": "test"}"""
        };

        // Act
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "I'll search for that.",
            ToolCalls = new List<ToolCall> { toolCall }
        };

        // Assert
        Assert.Single(message.ToolCalls);
        Assert.Equal("search", message.ToolCalls[0].Name);
        Assert.Equal("""{"query": "test"}""", message.ToolCalls[0].ArgumentsJson);
    }

    [Fact]
    public void ToolCall_ArgumentsAsJsonElement_ParsesCorrectly()
    {
        // Arrange
        var toolCall = new ToolCall
        {
            Name = "search",
            ArgumentsJson = """{"query": "test", "limit": 10}"""
        };

        // Act
        var args = toolCall.ArgumentsAsJsonElement();

        // Assert
        Assert.Equal("test", args.GetProperty("query").GetString());
        Assert.Equal(10, args.GetProperty("limit").GetInt32());
    }

    [Fact]
    public void ToolResult_FromObject_SerializesCorrectly()
    {
        // Arrange
        var result = new { value = 42, message = "success" };

        // Act
        var toolResult = ToolResult.FromObject("call_1", "calculator", result);

        // Assert
        Assert.Equal("call_1", toolResult.CallId);
        Assert.Equal("calculator", toolResult.Name);
        Assert.False(toolResult.IsError);
        Assert.Contains("42", toolResult.ResultJson);
        Assert.Contains("success", toolResult.ResultJson);
    }

    [Fact]
    public void Turn_EnumerateMessages_ReturnsCorrectOrder()
    {
        // Arrange
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Hello" },
            AssistantMessage = new Message { Role = Role.Assistant, Content = "Hi there!" },
            ToolMessages = new List<Message>
            {
                new() { Role = Role.Tool, Content = "Tool result" }
            }
        };

        // Act
        var messages = turn.EnumerateMessages().ToList();

        // Assert
        Assert.Equal(3, messages.Count);
        Assert.Equal(Role.User, messages[0].Role);
        Assert.Equal(Role.Assistant, messages[1].Role);
        Assert.Equal(Role.Tool, messages[2].Role);
    }

    [Fact]
    public void Conversation_AddTurn_UpdatesCorrectly()
    {
        // Arrange
        var conversation = new Conversation();
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Test" }
        };

        // Act
        conversation.AddTurn(turn);

        // Assert
        Assert.Single(conversation.Turns);
        Assert.Single(conversation.GetCachedMessages());
    }

    [Fact]
    public void Conversation_StateManagement_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();

        // Act
        conversation.SetState("user_name", "Alice");
        conversation.SetState("session_id", "12345");

        // Assert
        Assert.Equal("Alice", conversation.GetState<string>("user_name"));
        Assert.Equal("12345", conversation.GetState<string>("session_id"));
        Assert.Null(conversation.GetState<string>("nonexistent"));
    }

    [Fact]
    public void Conversation_GetCachedMessages_PerformsCorrectly()
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

        // Act
        var messages1 = conversation.GetCachedMessages();
        var messages2 = conversation.GetCachedMessages(); // Should use cache

        // Assert
        Assert.Equal(10, messages1.Count);
        Assert.Same(messages1, messages2); // Should be same reference due to caching
    }
}