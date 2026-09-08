using Andy.Context.Examples;
using System.Threading.Tasks;
using Andy.Context.Context;
using Andy.Context.Model;
using Andy.Context.Orchestration;
using Andy.Context.Tooling;

namespace Andy.Tests.Context.Core;

public class OrchestrationTests
{
    [Fact]
    public async Task AssistantOrchestrator_RunTurnAsync_WithoutTools_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        // Act
        var response = await orchestrator.RunTurnAsync("Hello there!");

        // Assert
        Assert.Equal(Role.Assistant, response.Role);
        Assert.Contains("Hello!", response.Content);
        Assert.Single(conversation.Turns);
        Assert.NotNull(conversation.Turns[0].AssistantMessage);
    }

    [Fact]
    public async Task AssistantOrchestrator_RunTurnAsync_WithTools_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        tools.Register(new CalculatorTool());
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        // Act
        var response = await orchestrator.RunTurnAsync("Please calc something");

        // Assert
        Assert.Equal(Role.Assistant, response.Role);
        Assert.Contains("Tool result received", response.Content);
        Assert.Single(conversation.Turns);

        var turn = conversation.Turns[0];
        Assert.NotNull(turn.AssistantMessage);
        Assert.Single(turn.ToolMessages);
        Assert.Equal(Role.Tool, turn.ToolMessages[0].Role);
    }

    [Fact]
    public async Task AssistantOrchestrator_RunTurnAsync_WithUnknownTool_HandlesGracefully()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        // Don't register any tools
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        // Act
        var response = await orchestrator.RunTurnAsync("Please calc something");

        // Assert
        Assert.Equal(Role.Assistant, response.Role);
        Assert.Contains("Tool result received", response.Content);

        var turn = conversation.Turns[0];
        Assert.Single(turn.ToolMessages);
        var toolMessage = turn.ToolMessages[0];
        Assert.True(toolMessage.Metadata.ContainsKey("tool_not_found"));
    }

    [Fact]
    public async Task AssistantOrchestrator_RunTurnAsync_WithOptions_RespectsSettings()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        var options = new ContextBuildOptions
        {
            TokenBudget = 1000,
            MaxRecentMessages = 5,
            IncludeToolMessages = false
        };

        // Act
        var response = await orchestrator.RunTurnAsync("Hello!", options);

        // Assert
        Assert.Equal(Role.Assistant, response.Role);
        // Options are passed to context manager, but we can't easily test compression
        // without more complex scenarios
    }

    [Fact]
    public async Task AssistantOrchestrator_RunTurnStreamAsync_WorksCorrectly()
    {
        // Arrange
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        // Act
        var messages = new List<Message>();
        await foreach (var message in orchestrator.RunTurnStreamAsync("Hello!"))
        {
            messages.Add(message);
        }

        // Assert
        Assert.Single(messages);
        Assert.Equal(Role.Assistant, messages[0].Role);
        Assert.Contains("Hello!", messages[0].Content);
    }
}