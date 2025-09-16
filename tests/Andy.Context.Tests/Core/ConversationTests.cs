using Andy.Context.Model;
using Andy.Context.Context;

namespace Andy.Tests.Context.Core;

public class ConversationTests
{
    [Fact]
    public void Conversation_ShouldInitializeEmpty()
    {
        // Arrange & Act
        var conversation = new Conversation();

        // Assert
        Assert.Empty(conversation.Turns);
        Assert.NotNull(conversation.Id);
        Assert.NotEqual(default(DateTimeOffset), conversation.CreatedAt);
        Assert.NotEqual(default(DateTimeOffset), conversation.LastActivityAt);
    }

    [Fact]
    public void AddTurn_ShouldAddTurnToConversation()
    {
        // Arrange
        var conversation = new Conversation();
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Hello, world!" }
        };

        // Act
        conversation.AddTurn(turn);

        // Assert
        Assert.Single(conversation.Turns);
        Assert.Equal(turn, conversation.Turns[0]);
        Assert.Equal(Role.User, conversation.Turns[0].UserOrSystemMessage.Role);
        Assert.Equal("Hello, world!", conversation.Turns[0].UserOrSystemMessage.Content);
    }

    [Fact]
    public void AddTurn_ShouldUpdateLastActivityTime()
    {
        // Arrange
        var conversation = new Conversation();
        var initialTime = conversation.LastActivityAt;
        var turn = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Test" }
        };

        // Act
        Thread.Sleep(10); // Ensure time difference
        conversation.AddTurn(turn);

        // Assert
        Assert.True(conversation.LastActivityAt > initialTime);
    }

    [Fact]
    public void ToChronoMessages_ShouldReturnMessagesInOrder()
    {
        // Arrange
        var conversation = new Conversation();

        // Add turn with user message and assistant response
        var turn1 = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Question 1" },
            AssistantMessage = new Message { Role = Role.Assistant, Content = "Answer 1" }
        };

        var turn2 = new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Question 2" },
            AssistantMessage = new Message { Role = Role.Assistant, Content = "Answer 2" }
        };

        conversation.AddTurn(turn1);
        conversation.AddTurn(turn2);

        // Act
        var messages = conversation.ToChronoMessages().ToList();

        // Assert
        Assert.Equal(4, messages.Count);
        Assert.Equal("Question 1", messages[0].Content);
        Assert.Equal("Answer 1", messages[1].Content);
        Assert.Equal("Question 2", messages[2].Content);
        Assert.Equal("Answer 2", messages[3].Content);
    }

    [Fact]
    public void GetCachedMessages_ShouldCacheResults()
    {
        // Arrange
        var conversation = new Conversation();
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Test" }
        });

        // Act
        var messages1 = conversation.GetCachedMessages();
        var messages2 = conversation.GetCachedMessages();

        // Assert
        Assert.Same(messages1, messages2); // Should return same instance
    }

    [Fact]
    public void InvalidateCache_ShouldClearCache()
    {
        // Arrange
        var conversation = new Conversation();
        conversation.AddTurn(new Turn
        {
            UserOrSystemMessage = new Message { Role = Role.User, Content = "Test" }
        });

        var messages1 = conversation.GetCachedMessages();

        // Act
        conversation.InvalidateCache();
        var messages2 = conversation.GetCachedMessages();

        // Assert
        Assert.NotSame(messages1, messages2); // Should return new instance
        Assert.Equal(messages1.Count, messages2.Count);
    }

    [Fact]
    public void StateManagement_ShouldStoreAndRetrieveState()
    {
        // Arrange
        var conversation = new Conversation();
        var testObject = new TestState { Value = "test", Number = 42 };

        // Act
        conversation.SetState("test-key", testObject);
        var retrieved = conversation.GetState<TestState>("test-key");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("test", retrieved.Value);
        Assert.Equal(42, retrieved.Number);
    }

    [Fact]
    public void StateManagement_ShouldReturnNullForMissingKey()
    {
        // Arrange
        var conversation = new Conversation();

        // Act
        var retrieved = conversation.GetState<TestState>("non-existent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void ClearState_ShouldRemoveAllState()
    {
        // Arrange
        var conversation = new Conversation();
        conversation.SetState("key1", new TestState { Value = "test1" });
        conversation.SetState("key2", new TestState { Value = "test2" });

        // Act
        conversation.ClearState();

        // Assert
        Assert.Null(conversation.GetState<TestState>("key1"));
        Assert.Null(conversation.GetState<TestState>("key2"));
    }

    private class TestState
    {
        public string Value { get; set; } = "";
        public int Number { get; set; }
    }
}