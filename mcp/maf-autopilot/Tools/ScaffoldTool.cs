using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MafAutopilot.Tools;

/// <summary>
/// Scaffold tool — generate correct MAF 1.3.0 boilerplate for common patterns.
/// All templates enforce the hard constraints (no [StreamsMessage], ManagedIdentityCredential, etc.).
/// No LLM required — deterministic output.
///
///   maf_scaffold — generate executor, session-provider, workflow, or a2a-server boilerplate
/// </summary>
[McpServerToolType]
public sealed class ScaffoldTool
{
    [McpServerTool(Name = "maf_scaffold")]
    [Description("""
        Generate correct MAF 1.3.0 boilerplate code for common patterns.

        Templates:
          executor         — sealed partial class : Executor with [MessageHandler] method
                             Enforces: ValueTask<T> return, no [StreamsMessage]/[YieldsMessage]
                             Requires: Microsoft.Agents.AI.Workflows.Generators 1.3.0 NuGet package
          session-provider — AIContextProvider with ProviderSessionState<T> for session isolation
                             Enforces: session state never in instance fields
          workflow         — workflow builder skeleton with fan-out/fan-in topology
                             Enforces: AddFanInBarrierEdge(sources, target) — sources FIRST
          a2a-server       — Program.cs A2A server registration
                             Enforces: AddA2AServer, MapA2AHttpJson, ManagedIdentityCredential

        All templates follow MAF 1.3.0 hard constraints. Inline comments show which rule each
        line enforces so you can understand the pattern, not just copy-paste it.

        Examples:
          maf_scaffold executor className=Order inputType=OrderRequest outputType=OrderResult
          maf_scaffold session-provider className=User
          maf_scaffold workflow className=Pipeline inputType=PipelineInput outputType=PipelineResult
          maf_scaffold a2a-server className=MyAgent
        """)]
    public string MafScaffold(
        [Description("Template to generate. One of: executor, session-provider, workflow, a2a-server")] string template,
        [Description("Class name prefix (e.g. 'Order' → OrderExecutor). Defaults to 'My'.")] string className = "My",
        [Description("For executor/workflow: input message type name (e.g. 'OrderRequest'). Defaults to 'InputMessage'.")] string inputType = "InputMessage",
        [Description("For executor/workflow: output message type name (e.g. 'OrderResult'). Defaults to 'OutputMessage'.")] string outputType = "OutputMessage",
        [Description("C# namespace for the generated code. Defaults to 'MyApp.Agents'.")] string namespaceName = "MyApp.Agents")
    {
        return template.ToLowerInvariant() switch
        {
            "executor"         => ExecutorTemplate(className, inputType, outputType, namespaceName),
            "session-provider" => SessionProviderTemplate(className, namespaceName),
            "workflow"         => WorkflowTemplate(className, inputType, outputType, namespaceName),
            "a2a-server"       => A2AServerTemplate(className),
            _ => $"""
                Unknown template '{template}'.

                Available templates:
                  executor         — executor with [MessageHandler], correct return type
                  session-provider — AIContextProvider with ProviderSessionState<T>
                  workflow         — fan-out/fan-in workflow builder
                  a2a-server       — A2A server Program.cs registration

                Examples:
                  maf_scaffold executor className=Order inputType=OrderRequest outputType=OrderResult
                  maf_scaffold session-provider className=User
                  maf_scaffold workflow className=Pipeline
                  maf_scaffold a2a-server className=MyAgent
                """
        };
    }

    // ── Templates ────────────────────────────────────────────────────────────────

    private static string ExecutorTemplate(string name, string input, string output, string ns) => $$"""
        // {{name}}Executor.cs — MAF 1.3.0 executor
        //
        // Requires NuGet: Microsoft.Agents.AI.Workflows.Generators 1.3.0
        // The source generator completes this partial class at build time.
        //
        // MAF 1.3.0 rules applied:
        //   ✅ sealed partial class : Executor   (not ReflectingExecutor<T>)
        //   ✅ [MessageHandler] attribute         (not IMessageHandler<TIn,TOut> interface)
        //   ✅ Returns ValueTask<{{output}}>       (NOT void — void silently starves fan-in)
        //   ✅ No [StreamsMessage] or [YieldsMessage]  (removed in MAF 1.3.0, causes CS0246)

        using Microsoft.Agents.AI.Workflows;

        namespace {{ns}};

        public sealed partial class {{name}}Executor : Executor
        {
            [MessageHandler]
            public async ValueTask<{{output}}> HandleAsync(
                {{input}} input,
                CancellationToken cancellationToken = default)
            {
                // TODO: implement your message processing logic
                await Task.Yield(); // replace with actual async work

                return new {{output}}(/* TODO: populate from input */);
            }
        }

        // Message types — define in separate files or replace with your existing types
        public record {{input}}(/* TODO: add properties */);
        public record {{output}}(/* TODO: add properties */);
        """;

    private static string SessionProviderTemplate(string name, string ns) => $$"""
        // {{name}}ContextProvider.cs — MAF 1.3.0 session-aware context provider
        //
        // MAF 1.3.0 rules applied:
        //   ✅ ProviderSessionState<T> for per-session data  (NEVER instance fields)
        //   ✅ AgentSession parameter (not AgentThread)

        using Microsoft.Agents.AI;

        namespace {{ns}};

        /// <summary>
        /// Supplies per-session context to the agent.
        ///
        /// ⚠️  NEVER store session data in instance fields — they are shared across ALL sessions.
        ///     ALWAYS use ProviderSessionState&lt;T&gt; to isolate state per active session.
        /// </summary>
        public sealed class {{name}}ContextProvider : AIContextProvider
        {
            // ✅ Session state injected via ProviderSessionState<T> — NOT a plain instance field
            private readonly ProviderSessionState<{{name}}SessionState> _state;

            public {{name}}ContextProvider(ProviderSessionState<{{name}}SessionState> state)
            {
                _state = state;
            }

            public override async Task<AIContext> GetContextAsync(
                AgentSession session,
                CancellationToken cancellationToken = default)
            {
                var state = await _state.GetOrCreateAsync(
                    session,
                    () => new {{name}}SessionState(),
                    cancellationToken);

                return new AIContext
                {
                    // TODO: build context from session state
                    Instructions = $"Session {session.SessionId}. {state.ExampleField}",
                };
            }
        }

        /// <summary>Per-session state — one instance per active session, isolated by ProviderSessionState&lt;T&gt;.</summary>
        public sealed class {{name}}SessionState
        {
            // TODO: add your session-specific fields here
            public string ExampleField { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        }
        """;

    private static string WorkflowTemplate(string name, string input, string output, string ns) => $$"""
        // {{name}}Workflow.cs — MAF 1.3.0 fan-out/fan-in workflow
        //
        // Requires NuGet: Microsoft.Agents.AI.Workflows.Generators 1.3.0 (source generator)
        //
        // MAF 1.3.0 rules applied:
        //   ✅ AddFanInBarrierEdge(sources, target)  — sources FIRST (common bug: reversed args)
        //   ✅ Fan-out handlers return ValueTask<T>   — void SILENTLY starves the fan-in barrier
        //   ✅ No [StreamsMessage] or [YieldsMessage]  (removed in MAF 1.3.0)

        using Microsoft.Agents.AI.Workflows;

        namespace {{ns}};

        public static class {{name}}WorkflowBuilder
        {
            public static IWorkflowBuilder Configure(IWorkflowBuilder builder)
            {
                var start    = builder.AddExecutor<{{name}}StartExecutor>();
                var branch1  = builder.AddExecutor<{{name}}Branch1Executor>();
                var branch2  = builder.AddExecutor<{{name}}Branch2Executor>();
                var finalize = builder.AddExecutor<{{name}}FinalizeExecutor>();

                // Fan-out: start sends to both branches
                builder.AddEdge(start, branch1);
                builder.AddEdge(start, branch2);

                // Fan-in: BOTH branches must complete before finalize runs
                // ✅ CORRECT: sources (IEnumerable) FIRST, then target
                // ❌ WRONG:   AddFanInBarrierEdge(finalize, new[]{branch1, branch2})
                builder.AddFanInBarrierEdge([branch1, branch2], finalize);

                return builder;
            }
        }

        // ── Executor stubs ────────────────────────────────────────────────────────────
        // Each executor: sealed partial class : Executor + [MessageHandler] method
        // Fan-out handlers MUST return ValueTask<T> — void causes SILENT fan-in starvation

        public sealed partial class {{name}}StartExecutor : Executor
        {
            [MessageHandler]
            public async ValueTask<{{input}}> HandleAsync({{input}} input, CancellationToken ct = default)
            {
                await Task.Yield();
                return input; // pass-through or transform before fan-out
            }
        }

        public sealed partial class {{name}}Branch1Executor : Executor
        {
            [MessageHandler]
            public async ValueTask<{{output}}> HandleAsync({{input}} input, CancellationToken ct = default)
            {
                await Task.Yield();
                return new {{output}}(/* TODO: branch 1 logic */);
            }
        }

        public sealed partial class {{name}}Branch2Executor : Executor
        {
            [MessageHandler]
            public async ValueTask<{{output}}> HandleAsync({{input}} input, CancellationToken ct = default)
            {
                await Task.Yield();
                return new {{output}}(/* TODO: branch 2 logic */);
            }
        }

        public sealed partial class {{name}}FinalizeExecutor : Executor
        {
            [MessageHandler]
            public async ValueTask<{{output}}> HandleAsync({{output}} result, CancellationToken ct = default)
            {
                await Task.Yield();
                return result; // TODO: aggregate or finalize
            }
        }

        public record {{input}}(/* TODO: add properties */);
        public record {{output}}(/* TODO: add properties */);
        """;

    private static string A2AServerTemplate(string name) => $$"""
        // Program.cs — MAF 1.3.0 A2A server registration
        //
        // MAF 1.3.0 rules applied:
        //   ✅ services.AddA2AServer(...)          (not AIAgentExtensions)
        //   ✅ app.MapA2AHttpJson("/a2a")           (not app.MapA2A(...))
        //   ✅ Instructions + Tools inside ChatOptions (not at agent top-level)
        //   ✅ ManagedIdentityCredential in prod    (not DefaultAzureCredential)

        using Azure.Identity;
        using Microsoft.Agents.A2A;
        using Microsoft.Agents.AI;

        var builder = WebApplication.CreateBuilder(args);

        // ── Agent configuration ──────────────────────────────────────────────────────
        // ✅ Instructions and Tools MUST be inside ChatOptions (not at top level)
        builder.Services.AddSingleton(new ChatClientAgentOptions
        {
            ChatOptions = new ChatOptions
            {
                Instructions = "You are {{name}}, a helpful assistant.",
                Tools = [],  // TODO: add your tool definitions here
            }
        });

        builder.Services.AddSingleton<ChatClientAgent>();

        // ── Credentials ──────────────────────────────────────────────────────────────
        // ✅ Use ManagedIdentityCredential in production — NEVER DefaultAzureCredential
        builder.Services.AddSingleton<TokenCredential>(
            builder.Environment.IsDevelopment()
                ? new DefaultAzureCredential()       // dev-only: local Azure CLI / VS
                : new ManagedIdentityCredential());   // production: managed identity

        // ── A2A server registration ──────────────────────────────────────────────────
        // ✅ Use AddA2AServer — NOT AIAgentExtensions (removed in 1.3.0)
        builder.Services.AddA2AServer(
            provider => provider.GetRequiredService<ChatClientAgent>(),
            new A2AServerRegistrationOptions
            {
                AgentCard = new AgentCard
                {
                    Name = "{{name}}",
                    Description = "TODO: describe your agent",
                    Url = builder.Configuration["AgentUrl"] ?? "https://localhost/a2a",
                }
            });

        var app = builder.Build();

        // ── A2A endpoints ────────────────────────────────────────────────────────────
        // ✅ Use MapA2AHttpJson or MapA2AJsonRpc — NOT app.MapA2A(...)
        app.MapA2AHttpJson("/a2a");
        // Alternative for JSON-RPC transport: app.MapA2AJsonRpc("/a2a");

        app.Run();
        """;
}
