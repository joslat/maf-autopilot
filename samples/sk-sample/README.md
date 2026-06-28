# `sk-sample` — a Semantic Kernel app to migrate FROM

A small, deliberately representative **Semantic Kernel (C#)** app. It exists so the
cross-framework migration flow has a real on-disk fixture to point at:

```bash
maf-doctor migrate-scan samples/sk-sample
```

You should see an inventory tagged by migration strategy plus a complexity verdict:

- **🌉 bridgeable** — `WeatherTools.cs` (`[KernelFunction]` plugin).
- **🔁 rewrite** — `ClaimAgent.cs` (`Kernel`, `ChatCompletionAgent`, `AgentThread`,
  `ChatHistoryAgentThread`, `InvokeAsync`, `OpenAIPromptExecutionSettings`, `AgentInvokeOptions`).
- **🏗 re-architect** — `Triage.cs` (`AgentGroupChat`, `FunctionCallingStepwisePlanner`).

Two distinct re-architecture kinds (group chat + planner) → the verdict is **HARD**.

> This is a **fixture**, not a runnable demo. It is not in `maf-autopilot.sln` and is
> not built by CI — `migrate-scan` reads it as source, it never compiles it. To turn it
> into MAF, run the `maf-migrate-from` prompt or `@maf-cross-migration` — they scaffold a
> NEW `SkSample.Maf` project beside this one and port it construct-by-construct,
> non-destructively. The full mapping is in
> [`docs/migration/from-semantic-kernel-to-maf.md`](../../docs/migration/from-semantic-kernel-to-maf.md).
