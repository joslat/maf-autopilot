---
description: "Always-loaded production-deployment patterns for MAF 1.3.0. Auto-applies to Program.cs, DI registration files, and infra config. Covers ManagedIdentityCredential, MaxTokens caps, secret handling, OpenTelemetry wiring, and the analyzer rules that catch regressions at write time."
applyTo: "**/Program.cs,**/Startup.cs,**/Hosting/*.cs,**/Infrastructure/*.cs"
---

# MAF Production Deployment — Patterns That Hold Up

This file is loaded when you edit a file that wires MAF into a host process (`Program.cs`, DI registration, infrastructure). The rules below are the ones we've learned the hard way — every one has a production incident behind it.

## Identity — the rule that catches half of all incidents

```csharp
// ❌ NEVER in production
services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

// ✅ Production
services.AddSingleton<TokenCredential>(_ => new ManagedIdentityCredential());
```

**Why:** `DefaultAzureCredential` walks a chain of credential providers (env vars, CLI, VS, IDE, MSI). In dev that's convenient. In prod it's a footgun: a dev's stale `AZURE_*` env var leaking into the container, an unintended `az login` token surviving in the build image, and silent fallback when MSI isn't available all produce subtle, hard-to-trace auth failures. The Roslyn analyzer `MAF002` fires on any `new DefaultAzureCredential()` outside test code — keep it pinned to "warning" or escalate to "error" in CI.

When you NEED a fallback chain (e.g., for a tool that runs both locally and in the cloud), build the chain explicitly with `ChainedTokenCredential` — never rely on `DefaultAzureCredential`'s implicit chain.

## Cost — every chat-completion call needs a ceiling

```csharp
// ❌ Unbounded — production cost incident waiting to happen
_agent = new ChatClientAgent(client, new ChatClientAgentOptions
{
    ChatOptions = new ChatOptions { Instructions = "..." }
});

// ✅ Always cap MaxOutputTokens
_agent = new ChatClientAgent(client, new ChatClientAgentOptions
{
    ChatOptions = new ChatOptions
    {
        MaxOutputTokens = 1024,        // hard ceiling — adjust per agent intent
        Instructions = "..."
    }
});
```

**Why:** A misbehaving prompt or an injection-induced "summarize this 30-page doc again" can blow through token budget in seconds. The `MafEstimateCost` tool surfaces every unbounded site; `MafDoctor` weights them into the grade. Default cap recommendation: 1024 for chat agents, 4096 for summarization agents, 8192+ ONLY for explicitly-scoped batch jobs.

For streaming responses, `MaxOutputTokens` still applies — the cap counts tokens, not delivery mode.

## Observability — OpenTelemetry must be wired BEFORE the agent

```csharp
services.AddSingleton<IChatClient>(sp =>
{
    var endpoint = new Uri(config["Azure:OpenAI:Endpoint"]!);
    var credential = sp.GetRequiredService<TokenCredential>();
    var rawClient = new AzureOpenAIClient(endpoint, credential)
        .AsChatClient(deploymentName: config["Azure:OpenAI:Deployment"]!);

    // Wrap with OpenTelemetry FIRST — every downstream call (including tool
    // invocations triggered by the model) flows through this wrapper.
    return rawClient.AsBuilder()
        .UseOpenTelemetry(sourceName: "MyApp.Agents")
        .Build();
});

// And the tracer provider must subscribe to that source name:
services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("MyApp.Agents")        // matches the sourceName above
        .AddOtlpExporter());
```

**Why:** If `UseOpenTelemetry(sourceName: X)` is wired but no `AddSource(X)` exists in the tracer provider, traces silently disappear — the diagnostic is "I see no traces at all," which is identical to "no traffic," which is the worst kind of false negative. Always grep for the source name string in both places.

## Secret handling — never these

```csharp
// ❌ Hard-coded — caught by anti-pattern rule MAF-AP-SEC-002
var key = "sk-1234567890abcdef...";

// ❌ Env var in code body
var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
services.AddSingleton(new ApiKey(key));

// ✅ Through configuration binding (Key Vault, Configuration Manager, etc.)
services.Configure<ApiOptions>(config.GetSection("OpenAI"));
services.AddSingleton<IApiKeyProvider, ConfigurationApiKeyProvider>();
```

**Why:** Secrets in `Environment.GetEnvironmentVariable` calls bypass the Configuration system — they can't be hot-swapped, they show up in process dumps, and they make secret rotation a redeploy. The Roslyn analyzer doesn't catch this (yet), but the prompt-lint rule PROMPT-004 will fire if a secret-looking string is concatenated into agent instructions.

## Sensitive data — never in non-dev

```csharp
// ❌ Catches MAF003 analyzer
var options = new ChatOptions { EnableSensitiveData = true };

// ✅ Configuration-gated
var options = new ChatOptions
{
    EnableSensitiveData = builder.Environment.IsDevelopment(),
};
```

**Why:** `EnableSensitiveData = true` logs the full chat history including PII and user input. In production this is a compliance violation (GDPR, HIPAA, depending on context) and a data-leak vector. The analyzer `MAF003` fires unconditionally outside test code.

## Session storage — never in instance fields

```csharp
// ❌ Per-instance state — incompatible with horizontal scaling
public sealed class MyContextProvider : AIContextProvider
{
    private string _userId = "";                   // BREAKS across replicas
    public override Task OnTurnStartedAsync(...) { ... }
}

// ✅ Use ProviderSessionState<T> — state-machine pattern, lives outside the instance
public sealed class MyContextProvider : AIContextProvider
{
    public override Task OnTurnStartedAsync(AgentTurnStartedContext ctx, ...)
    {
        var state = ctx.GetSessionState<MyState>();   // pulled from session store
        // ...
    }
}
```

**Why:** Instance fields on `AIContextProvider` / `ChatHistoryProvider` look fine in dev (single replica, single process). In prod, with multiple replicas behind a load balancer, requests for the same session can hit different processes — the instance state is wrong. This is constraint #2 in `maf://constraints` and not optional.

## Graceful shutdown — the host pattern

```csharp
var host = builder.Build();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStopping.Register(() =>
{
    // Allow in-flight RunAsync calls to complete. The default is to cancel them.
    // For chat agents this is usually fine. For long-running workflows, wait.
});

await host.RunAsync();
```

**Why:** A workflow mid-execution at shutdown can leave the fan-in barrier in a half-state. Always check whether the agent or workflow needs graceful drain.

## CI / deployment gates the team uses

Before any merge to `main`, the PR must pass:

- `MafDoctor(repoPath)` returns A or B. (Use `MafAuditPullRequest` for a scoped view.)
- The 3 Roslyn analyzers (`MAF001/002/003`) are pinned to "warning" minimum.
- The unit test suite passes (`dotnet test`).

Optional gates (recommended):

- `MafLintAgentPrompt(repoPath)` with no PROMPT-004 errors (prompt injection).
- `MafEstimateCost(repoPath)` with zero unbounded sites.

The CI sticky PR comment is the **product-scoped `doctor`** report (whole-repo, `samples/`/`.Tests/`/`find-the-bug/` excluded) run by `maf-pr-audit.yml` — no greenfield work should land without that summary being clean. (`MafAuditPullRequest` remains the on-demand MCP tool for a **diff-scoped** audit of just the changed files.)

## Anti-patterns we've already paid for

- **Secrets in `Instructions` literals.** Even "for testing" — once committed, they're committed. Use placeholders + runtime template substitution. PROMPT-004 catches some, but not all, of these.
- **`DefaultAzureCredential` "just for local dev"** that ships to prod because no one removed the `#if DEBUG`. Use config-gated `ChainedTokenCredential` instead.
- **`MaxIterations = int.MaxValue`** on tool-calling agents. The agent will loop, the costs will go up, your on-call gets paged. Always set a finite ceiling.
- **No retry policy on external tools.** A flaky downstream API surfaces as agent failure with no diagnostic. Wrap external `Func<>` tools in a retry handler.
