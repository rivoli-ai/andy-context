# Andy.Context Usage Examples

This project demonstrates comprehensive usage patterns for the **Andy.Context** library, showing how to build sophisticated conversation management systems with context compression and tool calling capabilities.

## Running the Examples

```bash
dotnet run
```

This will execute all example categories in sequence, demonstrating the library's capabilities without requiring a real LLM connection.

## Example Categories

### 1. Simple Examples

**Basic conversation management patterns:**

- **Basic Conversation Creation**: Creating conversations with system messages and user-assistant exchanges
- **Message Enumeration**: Iterating through conversation messages in chronological order
- **Conversation State Management**: Storing and retrieving custom state data
- **Conversation Statistics**: Getting detailed metrics about conversation content

### 2. Advanced Examples

**Sophisticated conversation patterns:**

- **Complex Conversation with Metadata**: Using rich metadata for tracking user context, response analytics, and session information
- **Message Linking and Threading**: Creating message hierarchies with parent-child relationships
- **Conversation Serialization**: Converting conversations to/from JSON for persistence

### 3. Context Management Examples

**Context compression and filtering strategies:**

- **Basic Context Building**: Simple context generation with token budgets and message limits
- **Simple Compression**: Using `SimpleCompressor` for basic message pruning
- **Smart Compression with Tool Preservation**: Using `SmartCompressor` to preserve tool call/result pairs during compression
- **Role Filtering**: Filtering messages by role (system, user, assistant, tool) for specific use cases

### 4. Tool Calling Examples

**Function execution and integration patterns:**

- **Basic Tool Registry**: Registering and managing available tools/functions
- **Manual Tool Calling Workflow**: Step-by-step tool execution with explicit result handling
- **Orchestrated Tool Calling**: Using `AssistantOrchestrator` for automated tool execution
- **Parallel Tool Calls**: Executing multiple tools concurrently for improved performance

## Key Concepts Demonstrated

### Conversation Architecture

- **Turn-based Structure**: Conversations are organized into turns containing user/system messages, assistant messages, and tool messages
- **Message Types**: Support for different message roles (System, User, Assistant, Tool) with rich metadata
- **State Management**: Built-in key-value state storage for conversation context

### Context Management

- **Token Budget Management**: Control context size based on estimated token usage
- **Compression Strategies**: Multiple algorithms for reducing context while preserving important information
- **Tool Call Preservation**: Smart compression that maintains tool call/result pairs for consistency

### Tool Integration

- **Declarative Tool Definitions**: JSON Schema-based tool parameter definitions
- **Automatic Validation**: Built-in validation of tool calls against their schemas
- **Error Handling**: Robust error handling with structured error results
- **Parallel Execution**: Support for concurrent tool execution

### LLM Integration

- **Vendor-Agnostic Interface**: Abstract `ILlmClient` interface for any LLM provider
- **Request/Response Models**: Structured request building and response handling
- **Streaming Support**: Built-in support for streaming LLM responses

## Architecture Overview

```
Conversation
├── Turns[]
│   ├── UserOrSystemMessage
│   ├── AssistantMessage (with optional ToolCalls)
│   └── ToolMessages[] (with ToolResults)
├── State Dictionary
└── Metadata

ContextManager
├── IContextCompressor (Simple/Smart)
├── ContextBuildOptions
└── Message Filtering & Compression

ToolRegistry
├── ITool[] (registered tools)
├── ToolDeclarations (JSON Schema)
└── Validation & Execution

AssistantOrchestrator
├── Conversation Management
├── Context Building
├── LLM Integration
└── Tool Execution
```

## Example Output

When you run the examples, you'll see output like:

```
=== Andy.Context Library Usage Examples ===

=== 1. SIMPLE EXAMPLES ===

1.1 Basic Conversation Creation:
Conversation ID: 55ad66f8f87345e4a426f8503972351c
Total turns: 2
Total messages: 3

1.2 Message Enumeration:
  [0] System: You are a helpful assistant.
  [1] User: Hello!
  [2] Assistant: Hi there! How can I help you?

...
```

## Integration Notes

- **No Real LLM Required**: Examples use `DemoLlmClient` for demonstration
- **Calculator Tool**: Includes a working calculator tool for arithmetic expressions
- **JSON Serialization**: All data structures are JSON-serializable for persistence
- **Thread-Safe**: Core library components are designed for concurrent use

This example project serves as both documentation and a testing ground for integrating the Andy.Context library into your own applications.