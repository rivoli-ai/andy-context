using System.Text.Json;
using Andy.Context.Model;
using Andy.Context.Context;
using Andy.Context.Tooling;
using Andy.Context.Orchestration;
using Andy.Context.Examples;
using Andy.Context.Utils;
using Andy.Context.Llm;

namespace Andy.Context.UsageExamples;

/// <summary>
/// Comprehensive examples demonstrating Andy.Context library usage.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Andy.Context Library Usage Examples ===\n");

        // Run examples in order
        await RunSimpleExamples();
        await RunAdvancedExamples();
        await RunContextManagementExamples();
        await RunToolCallingExamples();

        Console.WriteLine("\n=== All Examples Complete ===");
    }

    /// <summary>
    /// Simple examples showing basic conversation management.
    /// </summary>
    static async Task RunSimpleExamples()
    {
        Console.WriteLine("=== 1. SIMPLE EXAMPLES ===\n");

        // Example 1: Basic conversation creation
        Console.WriteLine("1.1 Basic Conversation Creation:");
        var conversation = new Conversation();

        // Add a system message
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.System,
                Content = "You are a helpful assistant."
            }
        });

        // Add a user-assistant exchange
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Hello!" },
            AssistantMessage = new Message { Role = Role.Assistant, Content = "Hi there! How can I help you?" }
        });

        Console.WriteLine($"Conversation ID: {conversation.Id}");
        Console.WriteLine($"Total turns: {conversation.Turns.Count}");
        Console.WriteLine($"Total messages: {conversation.GetCachedMessages().Count}");
        Console.WriteLine();

        // Example 2: Message enumeration
        Console.WriteLine("1.2 Message Enumeration:");
        var messages = conversation.GetCachedMessages();
        for (int i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            Console.WriteLine($"  [{i}] {msg.Role}: {msg.Content}");
        }
        Console.WriteLine();

        // Example 3: Conversation state management
        Console.WriteLine("1.3 Conversation State Management:");
        conversation.SetState("user_preferences", new { theme = "dark", language = "en" });
        conversation.SetState("session_info", new { start_time = DateTimeOffset.UtcNow });

        var preferences = conversation.GetState<object>("user_preferences");
        Console.WriteLine($"User preferences stored: {preferences != null}");
        Console.WriteLine();

        // Example 4: Conversation statistics
        Console.WriteLine("1.4 Conversation Statistics:");
        var stats = conversation.GetStats();
        Console.WriteLine($"  Total turns: {stats.TotalTurns}");
        Console.WriteLine($"  Total messages: {stats.TotalMessages}");
        Console.WriteLine($"  User messages: {stats.UserMessages}");
        Console.WriteLine($"  Assistant messages: {stats.AssistantMessages}");
        Console.WriteLine($"  System messages: {stats.SystemMessages}");
        Console.WriteLine($"  Duration: {stats.Duration}");
        Console.WriteLine();
    }

    /// <summary>
    /// Advanced examples showing sophisticated conversation patterns.
    /// </summary>
    static async Task RunAdvancedExamples()
    {
        Console.WriteLine("=== 2. ADVANCED EXAMPLES ===\n");

        // Example 1: Complex conversation with metadata
        Console.WriteLine("2.1 Complex Conversation with Metadata:");
        var conversation = new Conversation();

        // Add system message with metadata
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.System,
                Content = "You are an AI assistant specialized in helping with coding tasks.",
                Metadata = new Dictionary<string, object>
                {
                    ["version"] = "1.0",
                    ["capabilities"] = new[] { "code_review", "debugging", "optimization" }
                }
            }
        });

        // Add conversation with rich metadata
        var userMessage = new Message
        {
            Role = Role.User,
            Content = "Can you help me optimize this Python code?",
            Metadata = new Dictionary<string, object>
            {
                ["user_id"] = "user_123",
                ["session_id"] = "sess_456",
                ["language"] = "python",
                ["context"] = "code_optimization"
            }
        };

        var assistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = "I'd be happy to help optimize your Python code! Please share the code you'd like me to review.",
            Metadata = new Dictionary<string, object>
            {
                ["response_time_ms"] = 150,
                ["confidence"] = 0.95,
                ["suggested_follow_up"] = "code_sharing"
            }
        };

        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = userMessage,
            AssistantMessage = assistantMessage
        });

        Console.WriteLine($"Messages with metadata: {conversation.GetCachedMessages().Count(m => m.Metadata.Any())}");
        Console.WriteLine();

        // Example 2: Message linking and threading
        Console.WriteLine("2.2 Message Linking and Threading:");
        var parentMessage = new Message
        {
            Role = Role.User,
            Content = "What's the weather like?"
        };

        var followUpMessage = new Message
        {
            Role = Role.User,
            Content = "What about tomorrow?",
            ParentMessageId = parentMessage.Id
        };

        conversation.AddTurn(new Turn { UserOrSystemMessage = parentMessage });
        conversation.AddTurn(new Turn { UserOrSystemMessage = followUpMessage });

        Console.WriteLine($"Parent message ID: {parentMessage.Id}");
        Console.WriteLine($"Follow-up references parent: {followUpMessage.ParentMessageId == parentMessage.Id}");
        Console.WriteLine();

        // Example 3: Conversation serialization
        Console.WriteLine("2.3 Conversation Serialization:");
        var json = conversation.ToJson();
        var deserialized = ConversationExtensions.FromJson(json);

        Console.WriteLine($"Original conversation ID: {conversation.Id}");
        Console.WriteLine($"Deserialized conversation ID: {deserialized?.Id}");
        Console.WriteLine($"JSON size: {json.Length} characters");
        Console.WriteLine();
    }

    /// <summary>
    /// Context management examples showing compression and filtering.
    /// </summary>
    static async Task RunContextManagementExamples()
    {
        Console.WriteLine("=== 3. CONTEXT MANAGEMENT EXAMPLES ===\n");

        // Example 1: Basic context building
        Console.WriteLine("3.1 Basic Context Building:");
        var conversation = CreateLargeConversation(10);
        var contextManager = new ContextManager(conversation);

        var basicOptions = new ContextBuildOptions
        {
            TokenBudget = 1000,
            MaxRecentMessages = 5
        };

        var context = contextManager.Build(basicOptions);
        Console.WriteLine($"Original messages: {conversation.GetCachedMessages().Count}");
        Console.WriteLine($"Context messages: {context.Count}");
        Console.WriteLine();

        // Example 2: Simple compression
        Console.WriteLine("3.2 Simple Compression:");
        var simpleCompressor = new SimpleCompressor();
        var contextManagerSimple = new ContextManager(conversation, simpleCompressor);

        var simpleOptions = new ContextBuildOptions
        {
            TokenBudget = 500,
            MaxRecentMessages = 8,
            CompressionStrategy = CompressionStrategy.Simple
        };

        var simpleContext = contextManagerSimple.Build(simpleOptions);
        Console.WriteLine($"Simple compressed messages: {simpleContext.Count}");
        Console.WriteLine();

        // Example 3: Smart compression with tool preservation
        Console.WriteLine("3.3 Smart Compression with Tool Preservation:");
        var conversationWithTools = CreateConversationWithTools();
        var smartCompressor = new SmartCompressor();
        var contextManagerSmart = new ContextManager(conversationWithTools, smartCompressor);

        var smartOptions = new ContextBuildOptions
        {
            TokenBudget = 800,
            MaxRecentMessages = 6,
            PreserveToolCallPairs = true,
            CompressionStrategy = CompressionStrategy.Smart
        };

        var smartContext = contextManagerSmart.Build(smartOptions);
        Console.WriteLine($"Smart compressed messages: {smartContext.Count}");
        Console.WriteLine($"Tool calls preserved: {smartContext.Any(m => m.ToolCalls.Any())}");
        Console.WriteLine($"Tool results preserved: {smartContext.Any(m => m.ToolResults.Any())}");
        Console.WriteLine();

        // Example 4: Role filtering
        Console.WriteLine("3.4 Role Filtering:");
        var filterOptions = new ContextBuildOptions
        {
            IncludeSystemMessages = false,
            IncludeToolMessages = false,
            TokenBudget = 1000
        };

        var filteredContext = contextManager.Build(filterOptions);
        var roles = filteredContext.Select(m => m.Role).Distinct().ToList();
        Console.WriteLine($"Included roles: {string.Join(", ", roles)}");
        Console.WriteLine();
    }

    /// <summary>
    /// Tool calling examples showing function execution patterns.
    /// </summary>
    static async Task RunToolCallingExamples()
    {
        Console.WriteLine("=== 4. TOOL CALLING EXAMPLES ===\n");

        // Example 1: Basic tool registry
        Console.WriteLine("4.1 Basic Tool Registry:");
        var registry = new ToolRegistry();
        var calculator = new CalculatorTool();
        registry.Register(calculator);

        Console.WriteLine($"Registered tools: {registry.GetRegisteredToolNames().Count}");
        Console.WriteLine($"Calculator registered: {registry.IsRegistered("calculator")}");

        var declarations = registry.GetDeclaredTools();
        var calcDeclaration = declarations.First();
        Console.WriteLine($"Tool name: {calcDeclaration.Name}");
        Console.WriteLine($"Tool description: {calcDeclaration.Description}");
        Console.WriteLine();

        // Example 2: Manual tool calling workflow
        Console.WriteLine("4.2 Manual Tool Calling Workflow:");
        var conversation = new Conversation();

        // User asks for calculation
        var userMessage = new Message { Role = Role.User, Content = "What's 15 * 8?" };

        // Assistant responds with tool call
        var assistantMessage = new Message
        {
            Role = Role.Assistant,
            Content = "I'll calculate that for you.",
            ToolCalls = new List<ToolCall>
            {
                new ToolCall
                {
                    Id = "calc_1",
                    Name = "calculator",
                    ArgumentsJson = JsonSerializer.Serialize(new { expression = "15*8" })
                }
            }
        };

        // Execute the tool
        var toolCall = assistantMessage.ToolCalls.First();
        var toolResult = await calculator.ExecuteAsync(toolCall, CancellationToken.None);

        // Create tool message
        var toolMessage = new Message
        {
            Role = Role.Tool,
            Content = toolResult.ResultJson,
            ToolResults = new List<ToolResult> { toolResult }
        };

        // Build the turn
        var turn = new Turn
        {
            UserOrSystemMessage = userMessage,
            AssistantMessage = assistantMessage
        };
        turn.ToolMessages.Add(toolMessage);

        conversation.AddTurn(turn);

        Console.WriteLine($"Tool call ID: {toolCall.Id}");
        Console.WriteLine($"Tool result: {toolResult.ResultJson}");
        Console.WriteLine($"Is error: {toolResult.IsError}");
        Console.WriteLine();

        // Example 3: Orchestrated tool calling
        Console.WriteLine("4.3 Orchestrated Tool Calling:");
        var orchestratedConversation = new Conversation();
        var llmClient = new DemoLlmClient();
        var orchestrator = new AssistantOrchestrator(orchestratedConversation, registry, llmClient);

        var response = await orchestrator.RunTurnAsync("Calculate 25 + 17", new ContextBuildOptions());

        Console.WriteLine($"Response role: {response.Role}");
        Console.WriteLine($"Response content: {response.Content}");

        var finalStats = orchestratedConversation.GetStats();
        Console.WriteLine($"Total tool calls executed: {finalStats.ToolCalls}");
        Console.WriteLine($"Total tool results: {finalStats.ToolResults}");
        Console.WriteLine();

        // Example 4: Parallel tool calls
        Console.WriteLine("4.4 Parallel Tool Calls Pattern:");
        var parallelTurn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Calculate both 10+5 and 20*3" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "I'll calculate both expressions for you.",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall { Id = "calc_a", Name = "calculator", ArgumentsJson = JsonSerializer.Serialize(new { expression = "10+5" }) },
                    new ToolCall { Id = "calc_b", Name = "calculator", ArgumentsJson = JsonSerializer.Serialize(new { expression = "20*3" }) }
                }
            }
        };

        // Simulate parallel execution
        var tasks = parallelTurn.AssistantMessage.ToolCalls.Select(async tc =>
            await calculator.ExecuteAsync(tc, CancellationToken.None)).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            parallelTurn.ToolMessages.Add(new Message
            {
                Role = Role.Tool,
                Content = result.ResultJson,
                ToolResults = new List<ToolResult> { result }
            });
        }

        Console.WriteLine($"Parallel tool calls: {parallelTurn.AssistantMessage.ToolCalls.Count}");
        Console.WriteLine($"Tool results: {parallelTurn.ToolMessages.Count}");
        Console.WriteLine();
    }

    /// <summary>
    /// Helper method to create a large conversation for testing.
    /// </summary>
    static Conversation CreateLargeConversation(int exchangeCount)
    {
        var conversation = new Conversation();

        // Add system message
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message
            {
                Role = Role.System,
                Content = "You are a helpful assistant that provides detailed responses."
            }
        });

        // Add multiple exchanges
        for (int i = 1; i <= exchangeCount; i++)
        {
            conversation.AddTurn(new Turn
            {
                UserOrSystemMessage = new Message
                {
                    Role = Role.User,
                    Content = $"This is user message number {i}. It contains some sample text to simulate a longer conversation history."
                },
                AssistantMessage = new Message
                {
                    Role = Role.Assistant,
                    Content = $"This is assistant response number {i}. I'm providing a detailed response that includes helpful information and context for the user's question."
                }
            });
        }

        return conversation;
    }

    /// <summary>
    /// Helper method to create a conversation with tool calls.
    /// </summary>
    static Conversation CreateConversationWithTools()
    {
        var conversation = new Conversation();

        // Regular conversation
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Hello!" },
            AssistantMessage = new Message { Role = Role.Assistant, Content = "Hi! How can I help you?" }
        });

        // Tool calling turn
        var toolTurn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "What's 5 + 3?" },
            AssistantMessage = new Message
            {
                Role = Role.Assistant,
                Content = "I'll calculate that for you.",
                ToolCalls = new List<ToolCall>
                {
                    new ToolCall
                    {
                        Id = "calc_123",
                        Name = "calculator",
                        ArgumentsJson = JsonSerializer.Serialize(new { expression = "5+3" })
                    }
                }
            }
        };

        toolTurn.ToolMessages.Add(new Message
        {
            Role = Role.Tool,
            Content = JsonSerializer.Serialize(new { result = 8 }),
            ToolResults = new List<ToolResult>
            {
                ToolResult.FromObject("calc_123", "calculator", new { result = 8 })
            }
        });

        conversation.AddTurn(toolTurn);

        // More regular conversation
        for (int i = 0; i < 5; i++)
        {
            conversation.AddTurn(new Turn
            {
                UserOrSystemMessage = new Message { Role = Role.User, Content = $"Follow-up question {i + 1}" },
                AssistantMessage = new Message { Role = Role.Assistant, Content = $"Response to follow-up {i + 1}" }
            });
        }

        return conversation;
    }
}
