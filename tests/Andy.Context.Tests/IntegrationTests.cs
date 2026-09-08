using Andy.Context.Examples;
using System.Threading.Tasks;
using Andy.Context.Context;
using Andy.Context.Model;
using Andy.Context.Orchestration;
using Andy.Context.Tooling;
using Andy.Context.Utils;

namespace Andy.Tests.Context;

public class IntegrationTests
{
    [Fact]
    public async Task CompleteWorkflow_WithTools_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        tools.Register(new CalculatorTool());
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        // Add system prompt
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.System,
                Content = "You are a helpful assistant."
            }
        });

        var options = new ContextBuildOptions
        {
            TokenBudget = 1000,
            MaxRecentMessages = 10
        };

        // Act - Run multiple turns
        var response1 = await orchestrator.RunTurnAsync("Hello!", options);
        var response2 = await orchestrator.RunTurnAsync("Please calc something", options);
        var response3 = await orchestrator.RunTurnAsync("Thanks!", options);

        // Assert
        Assert.Equal(Role.Assistant, response1.Role);
        Assert.Equal(Role.Assistant, response2.Role);
        Assert.Equal(Role.Assistant, response3.Role);

        // Check conversation state
        Assert.Equal(4, conversation.Turns.Count); // System + 3 user turns
        var stats = conversation.GetStats();
        Assert.True(stats.TotalMessages > 0);
        Assert.True(stats.ToolCalls > 0);
        Assert.True(stats.ToolResults > 0);
    }

    [Fact]
    public async Task StreamingWorkflow_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        var options = new ContextBuildOptions();

        // Act
        var messages = new List<Message>();
        await foreach (var message in orchestrator.RunTurnStreamAsync("Hello!", options))
        {
            messages.Add(message);
        }

        // Assert
        Assert.Single(messages);
        Assert.Equal(Role.Assistant, messages[0].Role);
        Assert.Contains("Hello!", messages[0].Content);
    }

    [Fact]
    public void ContextCompression_PreservesToolPairs()
    {
        // Arrange
        var conversation = new Conversation();
        var compressor = new SmartCompressor();

        // Add a turn with tool calls
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Calculate 2+2" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "I'll calculate that",
                ToolCalls = new List<ToolCall> { new() { Id = "call_1", Name = "calculator" } }
            },
            ToolMessages = new List<Message>
            {
                new()
                {
                    Role = Role.Tool,
                    Content = "4",
                    ToolResults = new List<ToolResult> { new() { CallId = "call_1", Name = "calculator" } }
                }
            }
        });

        var options = new ContextBuildOptions
        {
            TokenBudget = 100,
            PreserveToolCallPairs = true
        };

        // Act
        var messages = conversation.GetCachedMessages();
        var compressed = compressor.Compress(messages, options);

        // Assert
        Assert.Contains(compressed, m => m.ToolCalls.Any());
        Assert.Contains(compressed, m => m.ToolResults.Any());
    }

    [Fact]
    public void Conversation_StateManagement_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();

        // Act
        conversation.SetState("user_id", "12345");
        conversation.SetState("session_id", "abc123");
        conversation.SetState("preferences", new { theme = "dark", language = "en" });

        // Assert
        Assert.Equal("12345", conversation.GetState<string>("user_id"));
        Assert.Equal("abc123", conversation.GetState<string>("session_id"));
        Assert.NotNull(conversation.GetState<object>("preferences"));
        Assert.Null(conversation.GetState<string>("nonexistent"));
    }

    [Fact]
    public void ToolRegistry_Management_WorksCorrectly()
    {
        // Arrange
        var registry = new ToolRegistry();
        var tool = new CalculatorTool();

        // Act
        registry.Register(tool);

        // Assert
        Assert.True(registry.IsRegistered("calculator"));
        Assert.True(registry.TryGet("calculator", out var retrievedTool));
        Assert.Same(tool, retrievedTool);
        Assert.Single(registry.GetDeclaredTools());
        Assert.Contains("calculator", registry.GetRegisteredToolNames());

        // Test unregistration
        registry.Unregister("calculator");
        Assert.False(registry.IsRegistered("calculator"));
        Assert.Empty(registry.GetDeclaredTools());
    }
}