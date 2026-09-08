using System.Text.Json;
using Andy.Context.Model;
using Andy.Context.Llm;
using Andy.Context.Tooling;
using Andy.Context.Orchestration;
using Andy.Context.Context;
using Andy.Context.Examples;
using Andy.Context.Utils;

namespace Andy.Tests.Context.Core;

/// <summary>
/// Tests for tool/function calling functionality
/// </summary>
public class ToolCallingTests
{
    [Fact]
    public async Task LlmClient_WithTools_ShouldReturnToolCalls()
    {
        // Arrange
        var llm = new DemoLlmClient();
        var messages = new List<Message>
        {
            new Message { Role = Role.User, Content = "Please calc something" }
        };

        var tools = new[]
        {
            new ToolDeclaration
            {
                Name = "calculator",
                Description = "Evaluates arithmetic expressions",
                Parameters = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["expression"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    ["required"] = new[] { "expression" }
                }
            }
        };

        // Act
        var request = new LlmRequest { Messages = messages, Tools = tools };
        var response = await llm.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.AssistantMessage);
        Assert.True(response.HasToolCalls);
        Assert.NotEmpty(response.AssistantMessage.ToolCalls);

        var toolCall = response.AssistantMessage.ToolCalls.First();
        Assert.Equal("calculator", toolCall.Name);
        Assert.NotNull(toolCall.Id);
        Assert.NotNull(toolCall.ArgumentsJson);
        // JSON may encode the + sign as \u002B
        Assert.Contains("expression", toolCall.ArgumentsJson);
    }

    [Fact]
    public void ToolCall_Creation_ShouldHaveRequiredFields()
    {
        // Arrange & Act
        var toolCall = new ToolCall
        {
            Id = "call_123",
            Name = "get_weather",
            ArgumentsJson = JsonSerializer.Serialize(new { location = "New York", units = "fahrenheit" })
        };

        // Assert
        Assert.Equal("call_123", toolCall.Id);
        Assert.Equal("get_weather", toolCall.Name);
        Assert.Contains("New York", toolCall.ArgumentsJson);

        // Should be able to parse arguments
        var element = toolCall.ArgumentsAsJsonElement();
        Assert.Equal("New York", element.GetProperty("location").GetString());
        Assert.Equal("fahrenheit", element.GetProperty("units").GetString());
    }

    [Fact]
    public void ToolResult_Creation_ShouldHandleVariousTypes()
    {
        // Arrange
        var successResult = new { temperature = 72, condition = "sunny", humidity = 65 };
        var errorResult = new { error = "Location not found", code = "LOC_404" };

        // Act
        var success = ToolResult.FromObject("call_123", "get_weather", successResult);
        var error = ToolResult.FromObject("call_456", "get_weather", errorResult, isError: true);

        // Assert - Success result
        Assert.Equal("call_123", success.CallId);
        Assert.Equal("get_weather", success.Name);
        Assert.False(success.IsError);
        Assert.Contains("72", success.ResultJson);
        Assert.Contains("sunny", success.ResultJson);

        // Assert - Error result
        Assert.Equal("call_456", error.CallId);
        Assert.Equal("get_weather", error.Name);
        Assert.True(error.IsError);
        Assert.Contains("Location not found", error.ResultJson);
        Assert.Contains("LOC_404", error.ResultJson);
    }

    [Fact]
    public void Message_WithToolCalls_ShouldSerializeCorrectly()
    {
        // Arrange
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "I'll check the weather for you",
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "call_weather",
                    Name = "get_weather",
                    ArgumentsJson = JsonSerializer.Serialize(new { location = "NYC" })
                },
                new ToolCall
                {
                    Id = "call_time",
                    Name = "get_time",
                    ArgumentsJson = JsonSerializer.Serialize(new { timezone = "EST" })
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize(message, JsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<Message>(json, JsonOptions.Default);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.ToolCalls.Count);
        Assert.Equal("get_weather", deserialized.ToolCalls[0].Name);
        Assert.Equal("get_time", deserialized.ToolCalls[1].Name);
        Assert.Contains("NYC", deserialized.ToolCalls[0].ArgumentsJson);
    }

    [Fact]
    public void Message_WithToolResults_ShouldHandleMultipleResults()
    {
        // Arrange
        var message = new Message
        {
            Role = Role.Tool,
            Content = "Tool execution results",
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("call_1", "weather", new { temp = 72 }),
                ToolResult.FromObject("call_2", "time", new { time = "3:45 PM" }),
                ToolResult.FromObject("call_3", "stocks", new { error = "Market closed" }, isError: true)
            }
        };

        // Assert
        Assert.Equal(3, message.ToolResults.Count);
        Assert.Equal("weather", message.ToolResults[0].Name);
        Assert.Equal("time", message.ToolResults[1].Name);
        Assert.Equal("stocks", message.ToolResults[2].Name);

        Assert.False(message.ToolResults[0].IsError);
        Assert.False(message.ToolResults[1].IsError);
        Assert.True(message.ToolResults[2].IsError);

        Assert.Contains("72", message.ToolResults[0].ResultJson);
        Assert.Contains("3:45 PM", message.ToolResults[1].ResultJson);
        Assert.Contains("Market closed", message.ToolResults[2].ResultJson);
    }

    [Fact]
    public void ToolRegistry_RegisterAndRetrieve_ShouldWork()
    {
        // Arrange
        var registry = new ToolRegistry();
        var calculator = new CalculatorTool();

        // Act
        registry.Register(calculator);

        // Assert - Can retrieve tool
        Assert.True(registry.IsRegistered("calculator"));
        Assert.True(registry.TryGet("calculator", out var retrieved));
        Assert.NotNull(retrieved);
        Assert.Equal("calculator", retrieved.Definition.Name);

        // Assert - Can get declarations
        var declarations = registry.GetDeclaredTools();
        Assert.Single(declarations);
        Assert.Equal("calculator", declarations[0].Name);
        Assert.Equal("Evaluates a basic arithmetic expression (e.g., '2+2*3').", declarations[0].Description);
        Assert.NotNull(declarations[0].Parameters);
    }

    [Fact]
    public void ToolDeclaration_WithComplexParameters_ShouldStructureCorrectly()
    {
        // Arrange & Act
        var declaration = new ToolDeclaration
        {
            Name = "search",
            Description = "Search for information",
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Search query"
                    },
                    ["filters"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["category"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["date_range"] = new Dictionary<string, object> { ["type"] = "string" }
                        }
                    },
                    ["limit"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["default"] = 10
                    }
                },
                ["required"] = new[] { "query" }
            },
            Required = false
        };

        // Assert
        Assert.Equal("search", declaration.Name);
        Assert.Equal("Search for information", declaration.Description);
        Assert.False(declaration.Required);

        var props = declaration.Parameters["properties"] as Dictionary<string, object>;
        Assert.NotNull(props);
        Assert.True(props.ContainsKey("query"));
        Assert.True(props.ContainsKey("filters"));
        Assert.True(props.ContainsKey("limit"));

        var required = declaration.Parameters["required"] as string[];
        Assert.NotNull(required);
        Assert.Contains("query", required);
    }

    [Fact]
    public async Task Orchestrator_WithToolCall_ShouldExecuteAndReturn()
    {
        // Arrange
        var conversation = new Conversation();
        var registry = new ToolRegistry();
        registry.Register(new CalculatorTool());
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, registry, llm);

        // Act
        var response = await orchestrator.RunTurnAsync("Please calc something", new ContextBuildOptions());

        // Assert
        Assert.NotNull(response);
        Assert.Equal(Role.Assistant, response.Role);

        // Check that tool was called
        var messages = conversation.GetCachedMessages();
        var hasToolCall = messages.Any(m => m.ToolCalls.Any());
        var hasToolResult = messages.Any(m => m.ToolResults.Any());

        Assert.True(hasToolCall, "Should have a message with tool calls");
        Assert.True(hasToolResult, "Should have a message with tool results");
    }

    [Fact]
    public void ToolCallValidator_WithValidCall_ShouldPass()
    {
        // Arrange
        var declaration = new ToolDeclaration
        {
            Name = "calculator",
            Description = "Calculate expression",
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["expression"] = new Dictionary<string, object> { ["type"] = "string" }
                },
                ["required"] = new[] { "expression" }
            }
        };

        var toolCall = new ToolCall
        {
            Id = "call_1",
            Name = "calculator",
            ArgumentsJson = JsonSerializer.Serialize(new { expression = "2+2" })
        };

        // Act
        var result = ToolCallValidator.Validate(toolCall, declaration);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Turn_WithCompleteToolInteraction_ShouldEnumerateInOrder()
    {
        // Arrange
        var turn = new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.User,
                Content = "What's the weather?"
            },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "Let me check the weather",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Id = "weather_call",
                        Name = "get_weather",
                        ArgumentsJson = JsonSerializer.Serialize(new { location = "Seattle" })
                    }
                }
            }
        };

        turn.ToolMessages.Add(new Message
        {
            Role = Role.Tool,
            Content = "Weather data",
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("weather_call", "get_weather",
                    new { temperature = 65, condition = "cloudy", precipitation = "10%" })
            }
        });

        // Act
        var messages = turn.EnumerateMessages().ToList();

        // Assert
        Assert.Equal(3, messages.Count);

        // User message
        Assert.Equal(Role.User, messages[0].Role);
        Assert.Equal("What's the weather?", messages[0].Content);

        // Assistant message with tool call
        Assert.Equal(Role.Assistant, messages[1].Role);
        Assert.Single(messages[1].ToolCalls);
        Assert.Equal("get_weather", messages[1].ToolCalls[0].Name);

        // Tool result message
        Assert.Equal(Role.Tool, messages[2].Role);
        Assert.Single(messages[2].ToolResults);
        Assert.Equal("weather_call", messages[2].ToolResults[0].CallId);
        Assert.Contains("65", messages[2].ToolResults[0].ResultJson);
    }

    [Fact]
    public void EmptyContent_WithToolCalls_ShouldBeValid()
    {
        // Arrange & Act
        var message = new Message
        {
            Role = Role.Assistant,
            Content = "", // Empty content when only making tool calls
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "calc_call",
                    Name = "calculator",
                    ArgumentsJson = JsonSerializer.Serialize(new { expression = "34*12" })
                }
            }
        };

        // Assert
        Assert.Empty(message.Content);
        Assert.NotEmpty(message.ToolCalls);
        Assert.Equal("calculator", message.ToolCalls[0].Name);

        // Should still be a valid message
        Assert.Equal(Role.Assistant, message.Role);
        Assert.NotNull(message.Id);
        Assert.NotEqual(default(DateTimeOffset), message.Timestamp);
    }

    [Fact]
    public async Task CallerManagedParallelResults_PreserveCallIds()
    {
        // Arrange
        var conversation = new Conversation();
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Get weather for NYC and Seattle" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "I'll check both locations",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Id = "nyc_weather",
                        Name = "get_weather",
                        ArgumentsJson = JsonSerializer.Serialize(new { location = "NYC" })
                    },
                    new ToolCall
                    {
                        Id = "seattle_weather",
                        Name = "get_weather",
                        ArgumentsJson = JsonSerializer.Serialize(new { location = "Seattle" })
                    }
                }
            }
        };

        // Simulate parallel execution results
        var nycResult = Task.Run(() => ToolResult.FromObject("nyc_weather", "get_weather",
            new { temperature = 75, condition = "sunny" }));
        var seattleResult = Task.Run(() => ToolResult.FromObject("seattle_weather", "get_weather",
            new { temperature = 65, condition = "rainy" }));

        // Act
        var results = await Task.WhenAll(nycResult, seattleResult);

        // Add results to turn
        foreach (var result in results)
        {
            turn.ToolMessages.Add(new Message
            {
                Role = Role.Tool,
                Content = result.ResultJson,
                ToolResults = new List<ToolResult> { result }
            });
        }

        conversation.AddTurn(turn);

        // Assert
        var messages = conversation.GetCachedMessages();
        Assert.Equal(4, messages.Count); // User, Assistant, 2 Tool messages

        var toolMessages = messages.Where(m => m.Role == Role.Tool).ToList();
        Assert.Equal(2, toolMessages.Count);

        var toolCallIds = turn.AssistantMessage.ToolCalls.Select(tc => tc.Id).ToList();
        var resultCallIds = toolMessages.SelectMany(m => m.ToolResults).Select(tr => tr.CallId).ToList();

        // All tool calls should have corresponding results
        Assert.All(toolCallIds, id => Assert.Contains(id, resultCallIds));
    }

    [Fact]
    public void ContextCompression_ShouldPreserveToolCallPairs()
    {
        // Arrange
        var conversation = new Conversation();

        // Add a turn with tool interaction
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Calculate 5+5" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "Calculating",
                ToolCalls = new List<ToolCall>
                {
                    new() { Id = "calc_1", Name = "calculator", ArgumentsJson = "{\"expression\":\"5+5\"}" }
                }
            }
        };

        turn.ToolMessages.Add(new Message
        {
            Role = Role.Tool,
            Content = "10",
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("calc_1", "calculator", new { result = 10 })
            }
        });

        conversation.AddTurn(turn);

        // Add more messages to trigger compression
        for (int i = 0; i < 10; i++)
        {
            conversation.AddTurn(new Turn
            {
                UserOrSystemMessage = new Message { Role = Role.User, Content = $"Message {i}" }
            });
        }

        var manager = new ContextManager(conversation, new SmartCompressor());
        var options = new ContextBuildOptions
        {
            TokenBudget = 100,
            PreserveToolCallPairs = true
        };

        // Act
        var context = manager.Build(options);

        // Assert
        // Should preserve the tool call/result pair even with compression
        var hasToolCall = context.Any(m => m.ToolCalls.Any(tc => tc.Id == "calc_1"));
        var hasToolResult = context.Any(m => m.ToolResults.Any(tr => tr.CallId == "calc_1"));

        Assert.Equal(hasToolCall, hasToolResult);
        Assert.True(context.Sum(TokenEstimator.Estimate) <= options.TokenBudget);
        Assert.Equal("Message 9", context.Last().Content);
    }
}