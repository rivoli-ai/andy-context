using System.Runtime.CompilerServices;
using System.Text.Json;
using Andy.Context.Context;
using Andy.Context.Llm;
using Andy.Context.Model;
using Andy.Context.Orchestration;
using Andy.Context.Tooling;
using Andy.Context.Utils;

namespace Andy.Tests.Context.Core;

public class ReviewRegressionTests
{
    [Fact]
    public void Persistence_RestoresCompleteHistoryAndTypedState()
    {
        var conversation = new Conversation();
        conversation.SetState("preferences", new Dictionary<string, string> { ["theme"] = "dark" });
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "compute", Id = "user" },
            AssistantMessage = CallMessage(),
            ToolMessages = new() { ResultMessage() },
            ContinuationMessages = new() { new Message { Role = Role.Assistant, Content = "done", ParentMessageId = "user" } }
        });
        var restored = ConversationExtensions.FromJson(conversation.ToJson());
        Assert.Single(restored.Turns);
        Assert.Equal(conversation.Id, restored.Id);
        Assert.Equal(conversation.CreatedAt, restored.CreatedAt);
        Assert.Equal(conversation.LastActivityAt, restored.LastActivityAt);
        Assert.Equal("dark", restored.GetState<Dictionary<string, string>>("preferences")!["theme"]);
        Assert.Equal(conversation.ToJson(), restored.ToJson());
        Assert.Equal("call", restored.Turns[0].AssistantMessage!.ToolCalls[0].Id);
        Assert.Equal("call", restored.Turns[0].ToolMessages[0].ToolResults[0].CallId);
        Assert.Equal("user", restored.Turns[0].ContinuationMessages[0].ParentMessageId);
    }

    [Fact]
    public void Persistence_RejectsMalformedJson()
        => Assert.Throws<JsonException>(() => ConversationExtensions.FromJson("{"));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Orchestration_PreservesHistoryAndSendsToolResult(bool streaming)
    {
        var client = new RecordingClient(CallMessage(), new Message { Role = Role.Assistant, Content = "done" });
        var conversation = new Conversation();
        var tools = new ToolRegistry();
        tools.Register(new TestTool());
        var orchestrator = new AssistantOrchestrator(conversation, tools, client);
        if (streaming)
            await foreach (var _ in orchestrator.RunTurnStreamAsync("compute")) { }
        else
            Assert.Equal("done", (await orchestrator.RunTurnAsync("compute")).Content);
        Assert.Equal(new[] { Role.User, Role.Assistant, Role.Tool }, client.Requests[1].Messages.Select(m => m.Role));
        Assert.Equal("call", client.Requests[1].Messages[2].ToolResults[0].CallId);
        var history = conversation.GetCachedMessages();
        Assert.Equal(new[] { Role.User, Role.Assistant, Role.Tool, Role.Assistant }, history.Select(m => m.Role));
        Assert.Equal("done", history[^1].Content);
        Assert.Empty(history[^1].ToolCalls);
        Assert.Equal(conversation.ToChronoMessages(), history);
        Assert.Equal(history.Select(m => m.Content),
            ConversationExtensions.FromJson(conversation.ToJson()).GetCachedMessages().Select(m => m.Content));
    }

    [Fact]
    public async Task Streaming_AccumulatesTextAndRetainsItForNextTurn()
    {
        var client = new RecordingClient(new Message { Role = Role.Assistant, Content = "hello" });
        var conversation = new Conversation();
        var orchestrator = new AssistantOrchestrator(conversation, new(), client);
        await foreach (var _ in orchestrator.RunTurnStreamAsync("first")) { }
        Assert.Equal("hello", conversation.Turns[0].AssistantMessage!.Content);
        await orchestrator.RunTurnAsync("second");
        Assert.Contains(client.Requests[1].Messages, m => m.Role == Role.Assistant && m.Content == "hello");
    }

    [Fact]
    public void SmartCompression_KeepsChronologyAcrossRecentBoundary()
    {
        var messages = Enumerable.Range(1, 3).Select(i => new Message { Role = Role.User, Content = $"user{i}" }).ToList();
        var actual = new SmartCompressor().Compress(messages, new() { MaxRecentMessages = 2 });
        Assert.Equal(messages.Where(actual.Contains), actual);
        Assert.Equal("user3", actual[^1].Content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(50)]
    public void SmartCompression_EnforcesTinyBudgets(int budget)
    {
        var messages = new List<Message> { new() { Role = Role.System, Content = new string('x', 1000) } };
        var actual = new SmartCompressor().Compress(messages, new() { TokenBudget = budget });
        Assert.True(actual.Sum(TokenEstimator.Estimate) <= budget);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compression_CountsToolPayloads(bool smart)
    {
        var message = new Message
        {
            Role = Role.Assistant,
            ToolCalls = new() {
            new ToolCall { Id = "huge", Name = "test", ArgumentsJson = new string('x', 2000) } }
        };
        IContextCompressor compressor = smart ? new SmartCompressor() : new SimpleCompressor();
        Assert.Empty(compressor.Compress(new() { message }, new() { TokenBudget = 100 }));
        Assert.True(TokenEstimator.Estimate(new Message
        {
            ToolResults = new() {
            new ToolResult { CallId = "huge", Name = "test", ResultJson = new string('x', 2000) } }
        }) > 100);
    }

    [Fact]
    public void SmartCompression_DropsCallAndResultTogether()
    {
        var old = new List<Message> { new() { Role = Role.User, Content = "old" }, CallMessage(), ResultMessage() };
        var latest = new Message { Role = Role.User, Content = "new" };
        old.Add(latest);
        var compressed = new SmartCompressor().Compress(old, new() { TokenBudget = TokenEstimator.Estimate(latest) });
        Assert.Equal(new[] { latest }, compressed);
    }

    internal static Message CallMessage(string id = "call") => new()
    {
        Role = Role.Assistant,
        ToolCalls = new() { new ToolCall { Id = id, Name = "test", ArgumentsJson = "{}" } }
    };
    internal static Message ResultMessage() => new()
    {
        Role = Role.Tool,
        ToolResults = new() { ToolResult.FromObject("call", "test", new { value = 42 }) }
    };

    internal sealed class TestTool : ITool
    {
        public ToolDeclaration Definition => new() { Name = "test", Description = "Test tool" };
        public Task<ToolResult> ExecuteAsync(ToolCall call, CancellationToken ct = default)
            => Task.FromResult(ToolResult.FromObject(call.Id, call.Name, new { value = 42 }));
    }

    internal sealed class RecordingClient(params Message[] replies) : ILlmClient
    {
        public List<LlmRequest> Requests { get; } = new();
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new LlmResponse { AssistantMessage = replies[Math.Min(Requests.Count - 1, replies.Length - 1)] });
        }
        public async IAsyncEnumerable<LlmStreamResponse> StreamCompleteAsync(LlmRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await CompleteAsync(request, cancellationToken);
            if (response.HasToolCalls)
                yield return new() { Delta = response.AssistantMessage };
            else
                foreach (var ch in response.AssistantMessage.Content)
                    yield return new() { Delta = new Message { Role = Role.Assistant, Content = ch.ToString() } };
        }
    }
}
