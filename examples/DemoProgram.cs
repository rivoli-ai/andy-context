using Andy.Context.Model;
using Andy.Context.Context;
using Andy.Context.Tooling;
using Andy.Context.Orchestration;
using Andy.Context.Utils;
using Andy.Context.Examples;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Examples;

/// <summary>
/// Demo program showing the LLM-agnostic conversation framework in action.
/// </summary>
public static class DemoProgram
{
    public static async Task Main()
    {
        Console.WriteLine("=== LLM-Agnostic Conversation Framework Demo ===\n");

        // 1. Create conversation and tools
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        tools.Register(new CalculatorTool());

        // 2. Create LLM client and orchestrator
        var llm = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

        // 3. Add system prompt
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.System,
                Content = "You are a helpful assistant that can perform calculations."
            }
        });

        // 4. Configure context options
        var options = new ContextBuildOptions
        {
            TokenBudget = 1200,
            MaxRecentMessages = 32,
            CompressionStrategy = CompressionStrategy.Smart
        };

        Console.WriteLine("Starting conversation...\n");

        // 5. Run several turns
        await RunTurn(orchestrator, "Hi there!", options);
        await RunTurn(orchestrator, "Please calc something for me", options);
        await RunTurn(orchestrator, "Thanks!", options);

        // 6. Show conversation statistics
        Console.WriteLine("\n=== Conversation Statistics ===");
        var stats = conversation.GetStats();
        Console.WriteLine($"Total turns: {stats.TotalTurns}");
        Console.WriteLine($"Total messages: {stats.TotalMessages}");
        Console.WriteLine($"User messages: {stats.UserMessages}");
        Console.WriteLine($"Assistant messages: {stats.AssistantMessages}");
        Console.WriteLine($"Tool messages: {stats.ToolMessages}");
        Console.WriteLine($"Tool calls: {stats.ToolCalls}");
        Console.WriteLine($"Tool results: {stats.ToolResults}");
        Console.WriteLine($"Tool errors: {stats.ToolErrors}");
        Console.WriteLine($"Duration: {stats.Duration}");

        // 7. Show conversation summary
        Console.WriteLine("\n=== Conversation Summary ===");
        Console.WriteLine(conversation.GetSummary());

        // 8. Demonstrate streaming
        Console.WriteLine("\n=== Streaming Demo ===");
        await RunStreamingTurn(orchestrator, "Tell me about streaming", options);
    }

    private static async Task RunTurn(AssistantOrchestrator orchestrator, string userInput, ContextBuildOptions options)
    {
        Console.WriteLine($"User: {userInput}");

        var response = await orchestrator.RunTurnAsync(userInput, options);
        Console.WriteLine($"Assistant: {response.Content}");

        Console.WriteLine();
    }

    private static async Task RunStreamingTurn(AssistantOrchestrator orchestrator, string userInput, ContextBuildOptions options)
    {
        Console.WriteLine($"User: {userInput}");
        Console.Write("Assistant: ");

        await foreach (var message in orchestrator.RunTurnStreamAsync(userInput, options))
        {
            Console.Write(message.Content);
            await Task.Delay(50); // Simulate streaming delay
        }

        Console.WriteLine("\n");
    }
}