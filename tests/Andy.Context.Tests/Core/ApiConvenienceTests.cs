using System.Text.Json;
using Andy.Context.Model;
using Andy.Context.Context;
using Andy.Context.Tooling;
using Andy.Context.Orchestration;
using Andy.Context.Examples;

namespace Andy.Tests.Context.Core;

/// <summary>
/// Tests for API convenience patterns and usage scenarios
/// </summary>
public class ApiConvenienceTests
{
    [Fact]
    public void CreateSimpleConversation_ShouldBeEasy()
    {
        // This test demonstrates how simple conversation creation should be
        // Arrange & Act
        var conversation = new Conversation();

        // Add messages in a natural way
        var systemTurn = CreateSystemTurn("You are a helpful assistant");
        var userTurn = CreateUserTurn("What's the capital of France?");

        conversation.AddTurn(systemTurn);
        conversation.AddTurn(userTurn);

        // Assert
        var messages = conversation.GetCachedMessages();
        Assert.Equal(2, messages.Count); // System and User
        Assert.Equal("You are a helpful assistant", messages[0].Content);
        Assert.Equal("What's the capital of France?", messages[1].Content);
    }

    [Fact]
    public void BuildRequest_ForLlmClient_ShouldIncludeNecessaryFields()
    {
        // Arrange
        var conversation = new Conversation();
        conversation.AddTurn(CreateSystemTurn("Be concise"));
        conversation.AddTurn(CreateUserTurn("Hello"));

        var tools = new ToolRegistry();
        tools.Register(new CalculatorTool());

        var contextManager = new ContextManager(conversation);
        var options = new ContextBuildOptions
        {
            TokenBudget = 1000,
            MaxRecentMessages = 10
        };

        // Act
        var messages = contextManager.Build(options);
        var toolDeclarations = tools.GetDeclaredTools();

        // Assert - Verify we have everything needed for an LLM request
        Assert.NotEmpty(messages);
        Assert.NotEmpty(toolDeclarations);

        // System message should be first
        Assert.Equal(Role.System, messages[0].Role);
        Assert.Equal("Be concise", messages[0].Content);

        // Tool declarations should have required fields
        var toolDecl = toolDeclarations[0];
        Assert.NotNull(toolDecl.Name);
        Assert.NotNull(toolDecl.Description);
        Assert.NotNull(toolDecl.Parameters);
    }

    [Fact]
    public void TokenBudgetEnforcement_ShouldLimitContext()
    {
        // Arrange
        var conversation = new Conversation();

        // Add many messages to exceed budget
        for (int i = 0; i < 100; i++)
        {
            conversation.AddTurn(CreateUserTurn($"This is message number {i} with some content"));
        }

        var contextManager = new ContextManager(conversation, new SimpleCompressor());
        var options = new ContextBuildOptions
        {
            TokenBudget = 50, // Very small budget
            MaxRecentMessages = 1000 // High limit to test token budget
        };

        // Act
        var context = contextManager.Build(options);

        // Assert
        // Should have dropped messages to stay under token budget
        Assert.True(context.Count < 100);

        // Estimate total tokens (roughly 4 chars per token)
        var estimatedTokens = context.Sum(TokenEstimator.Estimate);
        Assert.True(estimatedTokens <= options.TokenBudget);
    }

    [Fact]
    public void ConversationHistory_ShouldMaintainChronology()
    {
        // Arrange
        var conversation = new Conversation();
        var turns = new List<string>();

        // Act - Build conversation with specific order
        for (int i = 1; i <= 5; i++)
        {
            var turn = new Turn
            {
                UserOrSystemMessage = new Message { Role = Role.User, Content = $"Question {i}" },
                AssistantMessage = new Message { Role = Role.Assistant, Content = $"Answer {i}" }
            };
            conversation.AddTurn(turn);
            turns.Add($"Question {i}");
            turns.Add($"Answer {i}");
        }

        // Assert - Verify chronological order
        var messages = conversation.ToChronoMessages().ToList();
        Assert.Equal(10, messages.Count); // 5 questions + 5 answers

        for (int i = 0; i < turns.Count; i++)
        {
            Assert.Equal(turns[i], messages[i].Content);
        }
    }

    [Fact]
    public void ToolExecution_WithError_ShouldBeHandledGracefully()
    {
        // Arrange
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Calculate something" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "I'll calculate",
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "calc_1", Name = "calculator", ArgumentsJson = "{\"expression\":\"1/0\"}" }
                }
            }
        };

        // Act - Simulate tool error
        var errorResult = ToolResult.FromObject("calc_1", "calculator",
            new { error = "Division by zero", code = "MATH_ERROR" },
            isError: true);

        turn.ToolMessages.Add(new Message
        {
            Role = Role.Tool,
            Content = JsonSerializer.Serialize(new { error = "Division by zero" }),
            ToolResults = new List<ToolResult> { errorResult }
        });

        // Assert
        var toolMessage = turn.ToolMessages[0];
        Assert.Single(toolMessage.ToolResults);
        Assert.True(toolMessage.ToolResults[0].IsError);
        Assert.Contains("Division by zero", toolMessage.ToolResults[0].ResultJson);
    }

    [Fact]
    public void MessageMetadata_ShouldPersistThroughProcessing()
    {
        // Arrange
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "Response with metadata",
            Metadata = new Dictionary<string, object>
            {
                ["model"] = "gpt-4",
                ["temperature"] = 0.7,
                ["max_tokens"] = 500,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["custom_field"] = "custom_value"
            }
        };

        var conversation = new Conversation();
        conversation.AddTurn(new Turn { AssistantMessage = message });

        // Act
        var retrieved = conversation.GetCachedMessages().First(m => m.Role == Role.Assistant);

        // Assert
        Assert.Equal(5, retrieved.Metadata.Count);
        Assert.Equal("gpt-4", retrieved.Metadata["model"]);
        Assert.Equal(0.7, retrieved.Metadata["temperature"]);
        Assert.Equal(500, retrieved.Metadata["max_tokens"]);
        Assert.Equal("custom_value", retrieved.Metadata["custom_field"]);
    }

    [Fact]
    public void ParallelToolCalls_ShouldBeSupported()
    {
        // Arrange
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "I'll check weather and time simultaneously",
            ToolCalls = new List<ToolCall>
            {
                new()
                {
                    Id = "weather_call",
                    Name = "get_weather",
                    ArgumentsJson = JsonSerializer.Serialize(new { location = "Tokyo" })
                },
                new()
                {
                    Id = "time_call",
                    Name = "get_time",
                    ArgumentsJson = JsonSerializer.Serialize(new { timezone = "JST" })
                },
                new()
                {
                    Id = "news_call",
                    Name = "get_news",
                    ArgumentsJson = JsonSerializer.Serialize(new { category = "technology" })
                }
            }
        };

        // Act & Assert
        Assert.Equal(3, message.ToolCalls.Count);

        // Each call should be independent
        var calls = message.ToolCalls.Select(tc => tc.Name).ToList();
        Assert.Contains("get_weather", calls);
        Assert.Contains("get_time", calls);
        Assert.Contains("get_news", calls);

        // Each should have unique ID
        var ids = message.ToolCalls.Select(tc => tc.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public void LargeConversation_WithCompression_ShouldHandleEfficiently()
    {
        // Arrange
        var conversation = new Conversation();

        // Simulate a long conversation
        for (int i = 0; i < 50; i++)
        {
            var turn = new Turn
            {
                UserOrSystemMessage = new Message
                {
                    Role = Role.User,
                    Content = $"User question {i}: " + new string('x', 100) // 100+ chars each
                },
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = $"Assistant response {i}: " + new string('y', 100)
                }
            };
            conversation.AddTurn(turn);
        }

        // Use SimpleCompressor for predictable behavior in tests
        var contextManager = new ContextManager(conversation, new SimpleCompressor());
        var options = new ContextBuildOptions
        {
            TokenBudget = 500, // Limited budget for large conversation
            MaxRecentMessages = 10 // Limit messages
        };

        // Act
        var context = contextManager.Build(options);

        // Assert
        // SimpleCompressor should limit to MaxRecentMessages first
        Assert.True(context.Count <= options.MaxRecentMessages,
            $"Expected <= {options.MaxRecentMessages} messages but got {context.Count}");
        Assert.True(context.Count > 0, "Should have at least one message");

        // Should have compressed the conversation
        Assert.True(context.Count < 100, "Should have compressed from 100 to fewer messages");
    }

    // Helper methods to create turns/messages easily
    private static Turn CreateSystemTurn(string content)
    {
        return new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.System,
                Content = content
            }
        };
    }

    private static Turn CreateUserTurn(string content)
    {
        return new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.User,
                Content = content
            }
        };
    }
}