---
name: workflow-smoke-tester
description: "Generates minimal smoke test stubs for MAF 1.3.0 workflow patterns found in a codebase after migration. Covers fan-out/fan-in completeness, session round-trips, streaming, structured output, and tool invocation. These are structural sanity checks — not full integration tests — that catch silent runtime failures the build misses."
---

# workflow-smoke-tester

## Purpose

`dotnet build` passing does not mean the code works. The fan-in starvation bug is the canonical example: builds green, runs silently wrong, no error. Smoke tests add a fast, credential-free runtime check for the class of failures that static analysis alone cannot catch.

These tests use **mock agents** — no Azure credentials required, no network calls. They verify structural correctness, not AI output quality.

---

## When to Use This Skill

- After a migration is complete (Phase 3 / T11.x)
- Whenever workflow topology is modified
- As part of the post-migration `migration-retrospective`

---

## Pattern Detection

First, scan the codebase to find which patterns are present:

```powershell
# Fan-out/fan-in pattern
Select-String -Path "src/**/*.cs" -Pattern "AddFanOutEdge" -Recurse -List

# Streaming
Select-String -Path "src/**/*.cs" -Pattern "RunStreamingAsync" -Recurse -List

# Structured output
Select-String -Path "src/**/*.cs" -Pattern "RunAsync<" -Recurse -List

# Tool invocation / approval
Select-String -Path "src/**/*.cs" -Pattern "ApprovalRequiredAIFunction|FunctionApprovalRequestContent" -Recurse -List

# Session round-trip
Select-String -Path "src/**/*.cs" -Pattern "CreateSessionAsync|SerializeSessionAsync" -Recurse -List
```

Generate smoke tests only for patterns actually present.

---

## Smoke Test Templates

### Template 1 — Fan-out / Fan-in Completeness

**Verifies:** All N fan-out branches complete; the fan-in aggregator receives exactly N messages.

```csharp
// File: tests/SmokeTests/FanOutFanInSmokeTest.cs
[Fact]
public async Task FanOut_AllBranchesComplete_AggregatorReceivesAllMessages()
{
    // Arrange — use a mock chat client (no Azure credentials needed)
    var mockClient = new MockChatClient();
    var agent = mockClient.AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions { SystemPrompt = "Test agent" }
    });

    var session = await agent.CreateSessionAsync(CancellationToken.None);

    // Act
    var response = await agent.RunAsync("trigger fan-out workflow", session);

    // Assert — fan-in must have run (aggregated all branches)
    Assert.NotNull(response);
    Assert.NotEmpty(response.Text);  // fan-in produced output
    // Workflow-specific assertion: check aggregated result count
    // Assert.Equal(3, response.Result.BranchResults.Count);
}
```

**MockChatClient pattern:**
```csharp
public class MockChatClient : IChatClient
{
    public Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatCompletion(
            new ChatMessage(ChatRole.Assistant, "Mock response")));
    }

    public IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<StreamingChatCompletionUpdate>();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
```

### Template 2 — Session Round-Trip

**Verifies:** Session can be created, used, serialized, deserialized, and used again with continuity.

```csharp
[Fact]
public async Task Session_SerializeDeserialize_MaintainsContinuity()
{
    // Arrange
    var agent = new MockChatClient().AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions { SystemPrompt = "Test" }
    });

    // Act — first turn
    var session = await agent.CreateSessionAsync(CancellationToken.None);
    var response1 = await agent.RunAsync("Hello", session);
    Assert.NotNull(response1.Text);

    // Act — serialize and deserialize
    var serialized = await agent.SerializeSessionAsync(session);
    Assert.NotNull(serialized);
    Assert.NotEmpty(serialized);

    var restoredSession = await agent.DeserializeSessionAsync(serialized, CancellationToken.None);

    // Act — second turn on restored session
    var response2 = await agent.RunAsync("What did I say?", restoredSession);
    Assert.NotNull(response2.Text);
}
```

### Template 3 — Streaming Output

**Verifies:** `RunStreamingAsync()` produces at least one `AgentResponseUpdate` with non-empty text.

```csharp
[Fact]
public async Task Streaming_ProducesAtLeastOneUpdate()
{
    var agent = new MockStreamingChatClient().AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions { SystemPrompt = "Test" }
    });

    var session = await agent.CreateSessionAsync(CancellationToken.None);
    var updates = new List<AgentResponseUpdate>();

    await foreach (var update in agent.RunStreamingAsync("Hello", session, CancellationToken.None))
    {
        updates.Add(update);
    }

    Assert.NotEmpty(updates);
    Assert.Contains(updates, u => !string.IsNullOrEmpty(u.Text));
}
```

### Template 4 — Structured Output

**Verifies:** `RunAsync<T>()` returns a `T` that deserializes without error.

```csharp
[Fact]
public async Task StructuredOutput_ReturnsDeserializedResult()
{
    var agent = new MockStructuredChatClient<MyResult>().AsAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = new ChatOptions { SystemPrompt = "Test" }
    });

    var session = await agent.CreateSessionAsync(CancellationToken.None);
    var response = await ((ChatClientAgent)agent).RunAsync<MyResult>("Give me result", session);

    Assert.NotNull(response.Result);
    // Add type-specific assertions
}
```

---

## Test Project Setup

Add a smoke test project if one doesn't exist:

```xml
<!-- tests/<SolutionName>.SmokeTests/<SolutionName>.SmokeTests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
    <PackageReference Include="xunit" Version="2.7.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.7">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Agents.AI" Version="1.3.0" />
    <PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.3.0" />
    <PackageReference Include="Microsoft.Agents.AI.Workflows.Generators" Version="1.3.0">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.AI" Version="10.5.0" />
  </ItemGroup>
  <ItemGroup>
    <!-- Reference the main project -->
    <ProjectReference Include="../../src/<MainProject>/<MainProject>.csproj" />
  </ItemGroup>
</Project>
```

---

## Running Smoke Tests

```powershell
# Run smoke tests (no Azure credentials needed)
dotnet test tests/<SolutionName>.SmokeTests/ --logger "console;verbosity=normal"

# Run with coverage
dotnet test tests/<SolutionName>.SmokeTests/ --collect:"XPlat Code Coverage"
```

---

## Output Contract

This skill produces:
1. Generated test file(s) in `tests/<SolutionName>.SmokeTests/`
2. Test project `.csproj` (if not present)
3. A summary of which patterns were detected and which tests were generated

The smoke tests are committed alongside migration code changes — they serve as regression guards for future migrations.
