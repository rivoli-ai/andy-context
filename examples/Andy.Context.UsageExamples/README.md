# Andy.Context usage examples

From the repository root:

~~~bash
dotnet run --project examples/Andy.Context.UsageExamples/Andy.Context.UsageExamples.csproj
~~~

The .NET 10 console application demonstrates conversation creation, enumeration, state, statistics, message links, JSON round trips, context policies and tool execution. No real LLM credentials are needed; the mock calculator request uses a fixed expression.

The parallel example schedules tool tasks in application code. The built-in orchestrator executes sequentially. Sharing mutable conversations, turns or registries across threads requires application synchronization.

Streaming accumulates completed replies; interrupted streams can leave partial turns. Persistence supports JSON-compatible state, with untyped metadata restored as JsonElement. Context budgets are estimates and can drop complete turns. Validation implements a bounded JSON Schema subset.

See the [main README](../../README.md) for contracts, [schema support](../../docs/tool-validation.md), and [development instructions](../../docs/development.md).
