# Andy.Context - LLM-Agnostic Conversation Framework

A modern, LLM-agnostic conversation framework for building AI assistants with tool support, context management, and streaming capabilities.

## Features

- **LLM-Agnostic**: Works with any LLM provider (OpenAI, Anthropic, Azure, etc.)
- **Tool Support**: Declare and execute tools with proper error handling
- **Context Management**: Smart compression and token budgeting
- **Streaming**: Real-time response streaming
- **Turn-Based**: Logical conversation structure
- **No External Dependencies**: Pure .NET 10+ BCL implementation
- **Type-Safe**: Strong typing with proper error handling

## Quick Start

```csharp
using Andy.Context.Model;
using Andy.Context.Tooling;
using Andy.Context.Orchestration;

// 1. Create conversation and tools
var conversation = new Conversation();
var tools = new ToolRegistry();
tools.Register(new CalculatorTool());

// 2. Create LLM client and orchestrator
var llm = new DemoLlmClient();
var orchestrator = new AssistantOrchestrator(conversation, tools, llm);

// 3. Run a conversation turn
var response = await orchestrator.RunTurnAsync("Calculate 2+2");
Console.WriteLine(response.Content); // "The result is 4"
```

## Architecture

### Core Components

- **`Message`**: Unified message format with tool calls/results
- **`Conversation`**: Conversation state management with caching
- **`Turn`**: Groups related messages (user → assistant → tools → assistant)
- **`ToolRegistry`**: Centralized tool management
- **`ILlmClient`**: Vendor-agnostic LLM interface
- **`IContextCompressor`**: Smart context compression
- **`AssistantOrchestrator`**: Orchestrates the conversation flow

### Message Flow

```
User Message → LLM → Tool Calls → Tool Execution → Tool Results → LLM → Final Response
```

## Tool System

### Declaring Tools

```csharp
public class MyTool : ITool
{
    public ToolDeclaration Definition { get; } = new()
    {
        Name = "my_tool",
        Description = "Does something useful",
        Parameters = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["param"] = new Dictionary<string, object> { ["type"] = "string" }
            },
            ["required"] = new[] { "param" }
        }
    };

    public async Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default)
    {
        var args = call.ArgumentsAsJsonElement();
        var param = args.GetProperty("param").GetString();
        
        // Do work...
        var result = new { success = true, value = param };
        
        return ToolResult.FromObject(call.Id, Definition.Name, result);
    }
}
```

### Registering Tools

```csharp
var tools = new ToolRegistry();
tools.Register(new MyTool());
tools.Register(new AnotherTool());
```

## Context Management

### Smart Compression

The framework includes intelligent context compression that:

- Preserves tool call/result pairs
- Maintains conversation structure
- Respects token budgets
- Keeps recent messages intact

```csharp
var options = new ContextBuildOptions
{
    TokenBudget = 4000,
    MaxRecentMessages = 20,
    CompressionStrategy = CompressionStrategy.Smart,
    PreserveToolCallPairs = true
};

var context = contextManager.Build(options);
```

### Compression Strategies

- **`None`**: No compression
- **`Simple`**: Basic truncation
- **`Smart`**: Preserves structure and tool pairs
- **`Semantic`**: Future: AI-powered compression

## LLM Integration

### Implementing ILlmClient

```csharp
public class OpenAIClient : ILlmClient
{
    public async Task<LlmResponse> ChatAsync(
        IReadOnlyList<Message> context,
        IReadOnlyList<ToolDeclaration> declaredTools,
        CancellationToken ct = default)
    {
        // Convert to OpenAI format
        var messages = ConvertToOpenAIFormat(context);
        var tools = ConvertToOpenAIFormat(declaredTools);
        
        // Call OpenAI API
        var response = await _client.ChatCompletions.CreateAsync(/* ... */);
        
        // Convert back to framework format
        return ConvertFromOpenAIFormat(response);
    }

    public async IAsyncEnumerable<Message> ChatStreamAsync(/* ... */)
    {
        // Implement streaming
    }
}
```

## Streaming Support

```csharp
await foreach (var message in orchestrator.RunTurnStreamAsync("Hello!"))
{
    Console.Write(message.Content);
    await Task.Delay(50); // Simulate streaming delay
}
```

## Error Handling

The framework provides structured error handling:

```csharp
try
{
    var result = await tool.ExecuteAsync(call, ct);
}
catch (ToolExecutionException ex)
{
    // Structured tool execution error
    Console.WriteLine($"Tool {ex.ToolName} failed: {ex.Message}");
}
catch (RetryableToolException ex)
{
    // Retryable error with delay
    await Task.Delay(ex.RetryDelay);
    // Retry logic...
}
```

## Examples

See the `examples/` directory for:

- **`DemoProgram.cs`**: Complete working example
- **`DemoLlmClient.cs`**: Mock LLM for testing
- **`CalculatorTool.cs`**: Example tool implementation

## Testing

```bash
dotnet test
```

The framework includes comprehensive tests for:

- Message model functionality
- Tool execution and validation
- Context compression
- Orchestration flow
- Error handling

## Performance

- **Caching**: Conversation messages are cached for performance
- **Lazy Evaluation**: Context compression uses lazy evaluation
- **Memory Efficient**: Streaming support for large responses
- **Token Budgeting**: Smart token management

## License

MIT License - see LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

## Roadmap

- [ ] Semantic context compression
- [ ] Conversation persistence
- [ ] Advanced tool validation
- [ ] Multi-modal support
- [ ] Conversation analytics
- [ ] Performance monitoring