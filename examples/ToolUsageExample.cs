using Andy.Context;

namespace Examples;

/// <summary>
/// Example showing how to use the ContextManager with tools and system prompts
/// </summary>
public class ToolUsageExample
{
    public static void Main()
    {
        // 1. Create ContextManager with system prompt
        var contextManager = new ContextManager(
            "You are a helpful assistant that can search for information and get weather data."
        );

        // 2. Add available tools
        var searchTool = new ToolDeclaration
        {
            Name = "search",
            Description = "Search for information on the web",
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The search query"
                    }
                },
                ["required"] = new[] { "query" }
            }
        };

        var weatherTool = new ToolDeclaration
        {
            Name = "get_weather",
            Description = "Get current weather for a location",
            Parameters = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The city name"
                    }
                },
                ["required"] = new[] { "location" }
            }
        };

        contextManager.AddTool(searchTool);
        contextManager.AddTool(weatherTool);

        // 3. Simulate conversation
        contextManager.AddUserMessage("What's the weather like in Seattle?");
        contextManager.AddAssistantMessage("I'll get the weather for Seattle.");
        contextManager.AddToolExecution("get_weather", "call_1", 
            new Dictionary<string, object?> { ["location"] = "Seattle" }, 
            "Sunny, 72°F");
        contextManager.AddAssistantMessage("The weather in Seattle is sunny and 72°F.");

        // 4. Get context for LLM
        var context = contextManager.GetContext();

        // 5. The context now contains:
        // - System instruction: "You are a helpful assistant..."
        // - Available tools in LLM format
        // - Complete conversation history

        Console.WriteLine($"System: {context.SystemInstruction}");
        Console.WriteLine($"Available tools: {context.AvailableTools.Count}");
        Console.WriteLine($"Messages: {context.Messages.Count}");

        // 6. Get tools in OpenAI format for LLM API calls
        var openAITools = context.GetToolsInOpenAIFormat();
        Console.WriteLine($"OpenAI tools format: {openAITools.Count} tools");

        // 7. Get tools in Anthropic format for Claude API calls
        var anthropicTools = context.GetToolsInAnthropicFormat();
        Console.WriteLine($"Anthropic tools format: {anthropicTools.Count} tools");
    }
}