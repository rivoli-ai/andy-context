using System.Runtime.CompilerServices;
using System.Text.Json;
using Andy.Context.Context;
using Andy.Context.Llm;
using Andy.Context.Model;
using Andy.Context.Orchestration;
using Andy.Context.Tooling;
using static Andy.Tests.Context.Core.ReviewRegressionTests;

namespace Andy.Tests.Context.Core;

public class PolicyAndExecutionTests
{
    [Theory]
    [InlineData(CompressionStrategy.Smart)]
    [InlineData(CompressionStrategy.Simple)]
    [InlineData(CompressionStrategy.None)]
    public void Context_FiltersRolesAndActualMessageAge(CompressionStrategy strategy)
    {
        var messages = new List<Message>
        {
            new() { Role = Role.System, Content = "system" },
            new() { Role = Role.User, Content = "old", Timestamp = DateTimeOffset.UtcNow.AddHours(-2) },
            new() { Role = Role.Tool, Content = "tool" },
            new() { Role = Role.User, Content = "current" }
        };
        var actual = new SmartCompressor().Compress(messages, new()
        {
            CompressionStrategy = strategy,
            IncludeSystemMessages = false,
            IncludeToolMessages = false,
            MaxConversationAge = TimeSpan.FromHours(1)
        });
        Assert.Equal("current", Assert.Single(actual).Content);
    }

    [Fact]
    public void None_DoesNotApplyBudgetOrCount()
    {
        var messages = new List<Message> { new() { Role = Role.User, Content = "unchanged" } };
        Assert.Equal(messages, new SmartCompressor().Compress(messages,
            new() { CompressionStrategy = CompressionStrategy.None, TokenBudget = 0, MaxRecentMessages = 0 }));
    }

    [Theory]
    [InlineData(CompressionStrategy.Smart)]
    [InlineData(CompressionStrategy.Simple)]
    public void Context_CountLimitAndBudgetKeepRecentTail(CompressionStrategy strategy)
    {
        var messages = Enumerable.Range(0, 6).Select(i => new Message { Role = Role.User, Content = i.ToString() }).ToList();
        var actual = new SmartCompressor().Compress(messages, new() { CompressionStrategy = strategy, MaxRecentMessages = 2 });
        Assert.Equal(new[] { "4", "5" }, actual.Select(m => m.Content));
    }

    [Fact]
    public void Context_RejectsUnsupportedAndInvalidPolicies()
    {
        var compressor = new SmartCompressor();
        Assert.Throws<NotSupportedException>(() => compressor.Compress(new(), new() { CompressionStrategy = CompressionStrategy.Semantic }));
        Assert.Throws<ArgumentOutOfRangeException>(() => compressor.Compress(new(), new() { TokenBudget = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => compressor.Compress(new(), new() { MaxRecentMessages = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => compressor.Compress(new(), new() { MaxConversationAge = TimeSpan.FromSeconds(-1) }));
        Assert.Throws<ArgumentOutOfRangeException>(() => compressor.Compress(new(), new() { CompressionStrategy = (CompressionStrategy)50 }));
    }

    [Fact]
    public void RoleFilter_RemovesAssociatedCallsWhenResultsExcluded()
    {
        var actual = new SmartCompressor().Compress(new() { CallMessage(), ResultMessage() },
            new() { IncludeToolMessages = false });
        Assert.Empty(actual);
    }

    [Fact]
    public void SimpleTailCut_DoesNotLeaveOrphanedResults()
        => Assert.Empty(new SimpleCompressor().Compress(new() { CallMessage(), ResultMessage() },
            new() { MaxRecentMessages = 1 }));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Orchestrator_ExecutesMultipleRoundsAndPreservesEveryMessage(bool streaming)
    {
        var client = new RecordingClient(CallMessage("one"), CallMessage("two"), new() { Role = Role.Assistant, Content = "finished" });
        var conversation = new Conversation();
        var tools = new ToolRegistry(); tools.Register(new TestTool());
        var runner = new AssistantOrchestrator(conversation, tools, client);
        await Run(runner, streaming);
        Assert.Equal(3, client.Requests.Count);
        Assert.Equal(new[] { Role.User, Role.Assistant, Role.Tool, Role.Assistant, Role.Tool },
            client.Requests[2].Messages.Select(m => m.Role));
        Assert.Equal("finished", conversation.GetCachedMessages()[^1].Content);
        Assert.Equal(6, conversation.GetCachedMessages().Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolLimit_StopsModelLoopAndRecordsMatchingError(bool streaming)
    {
        var client = new RecordingClient(CallMessage());
        var conversation = new Conversation();
        var tools = new ToolRegistry(); tools.Register(new TestTool());
        var runner = new AssistantOrchestrator(conversation, tools, client, new() { MaxToolRounds = 1 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(runner, streaming));
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains("tool_round_limit", conversation.GetCachedMessages()[^1].Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_FromToolPropagatesWithoutCallingModelAgain(bool streaming)
    {
        var client = new RecordingClient(CallMessage());
        var tools = new ToolRegistry(); tools.Register(new DelegateTool((call, ct) => throw new OperationCanceledException(ct)));
        var runner = new AssistantOrchestrator(new(), tools, client);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(runner, streaming));
        Assert.Single(client.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancelledTurn_DoesNotMutateHistory(bool streaming)
    {
        var conversation = new Conversation();
        var client = new RecordingClient(CallMessage());
        var runner = new AssistantOrchestrator(conversation, new(), client);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Run(runner, streaming, cts.Token));
        Assert.Empty(conversation.Turns);
        Assert.Empty(client.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownTools_AreReturnedAsErrorsToModel(bool streaming)
    {
        var client = new RecordingClient(CallMessage(), new() { Role = Role.Assistant, Content = "done" });
        await Run(new(new(), new(), client), streaming);
        var result = client.Requests[1].Messages[^1].ToolResults.Single();
        Assert.True(result.IsError);
        Assert.Contains("tool_not_found", result.ResultJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SchemaFailure_DoesNotExecuteTool(bool streaming)
    {
        var client = new RecordingClient(CallMessage(), new() { Role = Role.Assistant, Content = "done" });
        var calls = 0;
        var tools = new ToolRegistry();
        tools.Register(new DelegateTool((call, ct) => { calls++; return Task.FromResult(new ToolResult()); },
            new() { ["type"] = "object", ["required"] = new[] { "expression" } }));
        await Run(new(new(), tools, client), streaming);
        Assert.Equal(0, calls);
        Assert.Contains("validation_failed", client.Requests[1].Messages[^1].Content);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolFailure_IsRecordedAndModelCanRecover(bool streaming)
    {
        var client = new RecordingClient(CallMessage(), new() { Role = Role.Assistant, Content = "recovered" });
        var tools = new ToolRegistry();
        tools.Register(new DelegateTool((call, ct) => throw new InvalidOperationException("tool broke")));
        await Run(new(new(), tools, client), streaming);
        Assert.True(client.Requests[1].Messages[^1].ToolResults.Single().IsError);
        Assert.Contains("tool broke", client.Requests[1].Messages[^1].Content);
    }

    [Fact]
    public async Task Streaming_AssemblesInterleavedArgumentFragmentsAndSkipsUsageChunks()
    {
        var client = new ChunkClient(
            new[] { Fragment("one", "test", "{"), Fragment("two", "test", "{"),
                Fragment("one", "", "\"x\":1}"), Fragment("two", "", "\"x\":2}"),
                new LlmStreamResponse { Usage = new(), IsComplete = true } },
            new[] { new LlmStreamResponse { Delta = new() { Role = Role.Assistant, Content = "done" } } });
        var executed = new List<string>();
        var tools = new ToolRegistry();
        tools.Register(new DelegateTool((call, ct) =>
        {
            executed.Add(call.ArgumentsJson);
            return Task.FromResult(ToolResult.FromObject(call.Id, call.Name, new { ok = true }));
        }));
        var conversation = new Conversation();
        var chunks = new List<Message>();
        await foreach (var chunk in new AssistantOrchestrator(conversation, tools, client).RunTurnStreamAsync("go"))
            chunks.Add(chunk);
        Assert.All(chunks, Assert.NotNull);
        Assert.Equal(new[] { "{\"x\":1}", "{\"x\":2}" }, executed);
        Assert.Equal(2, client.Requests[1].Messages.Count(m => m.Role == Role.Tool));
        Assert.Equal("done", conversation.GetCachedMessages()[^1].Content);
    }

    [Fact]
    public async Task Streaming_ProviderErrorThrowsAndReleasesTurnGuard()
    {
        var client = new ChunkClient(new[] { new LlmStreamResponse { Error = "provider unavailable" } },
            new[] { new LlmStreamResponse { Delta = new() { Role = Role.Assistant, Content = "retry" } } });
        var runner = new AssistantOrchestrator(new(), new(), client);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Run(runner, true));
        Assert.Contains("provider unavailable", error.Message);
        await Run(runner, true);
    }

    [Fact]
    public async Task Streaming_UsageOnlyStreamDoesNotInventAnAnswer()
    {
        var client = new ChunkClient(new[] { new LlmStreamResponse { Usage = new(), IsComplete = true } });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(new(new(), new(), client), true));
    }

    [Fact]
    public async Task EmptyContext_IsRejectedBeforeCallingProvider()
    {
        var client = new RecordingClient(CallMessage());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AssistantOrchestrator(new(), new(), client).RunTurnAsync("too large", new() { TokenBudget = 0 }));
        Assert.Empty(client.Requests);
    }

    [Fact]
    public async Task RequestConfig_IsForwarded()
    {
        var client = new RecordingClient(new Message { Role = Role.Assistant, Content = "done" });
        var config = new LlmClientConfig { Model = "test-model" };
        await new AssistantOrchestrator(new(), new(), client, new() { LlmConfig = config }).RunTurnAsync("go");
        Assert.Same(config, client.Requests[0].Config);
    }

    [Fact]
    public async Task SameOrchestrator_RejectsConcurrentTurnsAndReleasesAfterDisposal()
    {
        var client = new ChunkClient(new[] { new LlmStreamResponse { Delta = new() { Content = "partial" } } });
        var runner = new AssistantOrchestrator(new(), new(), client);
        var stream = runner.RunTurnStreamAsync("first").GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunTurnAsync("overlap"));
        await stream.DisposeAsync();
        await Run(runner, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Tools_RunSequentiallyUntilFirstCompletes(bool streaming)
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        var tools = new ToolRegistry();
        tools.Register(new DelegateTool(async (call, ct) =>
        {
            calls.Add(call.Id);
            if (call.Id == "first")
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(ct);
            }
            return ToolResult.FromObject(call.Id, call.Name, new { ok = true });
        }));
        var message = new Message
        {
            Role = Role.Assistant,
            ToolCalls = new()
        {
            new ToolCall { Id = "first", Name = "test" },
            new ToolCall { Id = "second", Name = "test" }
        }
        };
        var client = new RecordingClient(message, new Message { Role = Role.Assistant, Content = "done" });
        var running = Run(new(new(), tools, client), streaming);
        try
        {
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(new[] { "first" }, calls);
        }
        finally { releaseFirst.TrySetResult(); }
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "first", "second" }, calls);
    }

    [Fact]
    public async Task DemoClient_NewUserTurnDoesNotReuseAnOldToolResult()
    {
        var tools = new ToolRegistry(); tools.Register(new Andy.Context.Examples.CalculatorTool());
        var runner = new AssistantOrchestrator(new(), tools, new Andy.Context.Examples.DemoLlmClient());
        await runner.RunTurnAsync("calc");
        Assert.Contains("Hello!", (await runner.RunTurnAsync("thanks")).Content);
    }

    internal static async Task Run(AssistantOrchestrator runner, bool streaming, CancellationToken ct = default)
    {
        if (streaming) { await foreach (var _ in runner.RunTurnStreamAsync("go", ct: ct)) { } }
        else await runner.RunTurnAsync("go", ct: ct);
    }

    private static LlmStreamResponse Fragment(string id, string name, string args) => new()
    {
        Delta = new() { Role = Role.Assistant, ToolCalls = new() { new ToolCall { Id = id, Name = name, ArgumentsJson = args } } }
    };

    private sealed class DelegateTool(Func<ToolCall, CancellationToken, Task<ToolResult>> run,
        Dictionary<string, object>? parameters = null) : ITool
    {
        public ToolDeclaration Definition => new() { Name = "test", Description = "test", Parameters = parameters ?? new() };
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default) => run(call, ct);
    }

    private sealed class ChunkClient(params LlmStreamResponse[][] responses) : ILlmClient
    {
        public List<LlmRequest> Requests { get; } = new();
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public async IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            await Task.CompletedTask;
            foreach (var chunk in responses[Math.Min(Requests.Count - 1, responses.Length - 1)]) yield return chunk;
        }
    }
}
