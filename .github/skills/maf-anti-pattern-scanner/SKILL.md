---
name: maf-anti-pattern-scanner
description: "Scans a MAF 1.3.0 codebase for known anti-patterns AFTER migration is complete. Prefer the MCP tool MafScanAntiPatterns for one-shot scans; this skill is for understanding the rule taxonomy or contributing new anti-pattern rules. Detects insecure credential usage (DefaultAzureCredential, hard-coded keys), unsafe defaults (EnableSensitiveData=true in non-dev), thread-unsafe state in AIContextProvider, missing observability (UseOpenTelemetry not wired), and missing identity (ManagedIdentityCredential preferred over secret-based auth). Counterpart to migration tooling: this is steady-state best-practice review."
---

# maf-anti-pattern-scanner

The migration tooling answers "is this code on MAF 1.3.0?" This skill answers a different question: **"is this MAF 1.3.0 code following best practices?"** Same codebase, different lens.

## When to use

- After a migration plan finishes — verify the converted code isn't merely compiling, it's idiomatic.
- On any clean 1.3.0 codebase as a periodic audit.
- In CI on PRs touching MAF agent/workflow code, scoped to the diff.

## When NOT to use

- For migration discovery — that's `maf-obsolete-api-registry` + `cs0618-hunter`. This skill assumes you're already on 1.3.0.
- For runtime correctness checks — that's `maf-fan-out-validator`. This skill checks *idiom and configuration*, not topology correctness.

## Anti-patterns covered

Each is a "rule" with a unique ID, a `severity`, and a deterministic search pattern. The MCP tool `MafScanAntiPatterns` ingests this list.

### Security

#### `MAF-AP-SEC-001` — `DefaultAzureCredential` in production code  (severity: error)
**Pattern:** `new DefaultAzureCredential()` anywhere outside a test project (`*Tests.csproj`).
**Why:** `DefaultAzureCredential` walks an authentication chain that includes developer credentials, environment variables, and managed identity. The chain order can change between SDK versions; production deployments should NEVER tolerate that surprise. Pin to the specific credential type your environment uses.
**Fix:** Replace with `ManagedIdentityCredential` (Azure-hosted) or `WorkloadIdentityCredential` (Kubernetes), or `ClientSecretCredential` / `ClientCertificateCredential` for explicit service principals.
**Source:** `maf-constraints.instructions.md` hard rule #4.

#### `MAF-AP-SEC-002` — Hard-coded API keys  (severity: error)
**Pattern:** String literal matching `(sk-|api[-_]?key)[A-Za-z0-9_-]{16,}` anywhere in .cs files.
**Why:** Keys in source land in git history forever.
**Fix:** Read from `IConfiguration` / `Azure.Identity` / `Microsoft.Extensions.Configuration.UserSecrets`.

#### `MAF-AP-SEC-003` — `EnableSensitiveData = true` in non-dev  (severity: error)
**Pattern:** `EnableSensitiveData\s*=\s*true` in a file that is not under `tests/`, not under `samples/`, and not inside an `#if DEBUG` block.
**Why:** Sensitive-data logging in production leaks PII, prompts, and tool arguments to the log sink.
**Fix:** Gate behind `#if DEBUG` or `builder.Environment.IsDevelopment()`.
**Source:** `maf-constraints.instructions.md` hard rule #5.

### Concurrency / state

#### `MAF-AP-CONC-001` — Instance fields on `AIContextProvider`  (severity: error)
**Pattern:** A class deriving from `AIContextProvider` (or `ChatHistoryProvider`) that declares any non-readonly instance field.
**Why:** `AIContextProvider` instances are shared across sessions. Mutable instance state causes cross-session leakage, races, and non-deterministic responses.
**Fix:** Use `ProviderSessionState<T>` for per-session state, or make fields `readonly` and immutable.
**Source:** `maf-constraints.instructions.md` hard rule #2.

#### `MAF-AP-CONC-002` — `.Result` / `.Wait()` on async  (severity: warning)
**Pattern:** `\.Result\b` or `\.Wait\(\)` immediately following an awaitable expression in agent / executor code.
**Why:** Sync-over-async deadlocks under SynchronizationContext. Agent runtimes pump async; mixing sync waits is a latent hang.
**Fix:** `await` the expression. Make the calling method `async`.

### Observability

#### `MAF-AP-OBS-001` — Missing `UseOpenTelemetry`  (severity: warning)
**Pattern:** A file constructs an `AIAgentBuilder` / `ChatClientAgent` but never calls `UseOpenTelemetry(...)` (anywhere in the same file or any sibling configuration file).
**Why:** Without OTel, agent calls are invisible in production. Token usage, latency, errors all silent.
**Fix:** Add `.UseOpenTelemetry(otelOptions)` to the builder chain. Wire to your existing OTel exporter.
**Source:** Migration guide §13.

#### `MAF-AP-OBS-002` — `IChatClient` instantiated without telemetry  (severity: info)
**Pattern:** `new ChatClient(` or `new OpenAIChatClient(` not chained with `.UseOpenTelemetry()` before being passed to an agent builder.
**Why:** OpenTelemetry must wrap the client to capture token costs. Wrap at the lowest level so every agent benefits.

### Identity

#### `MAF-AP-ID-001` — Missing `ManagedIdentityCredential` for Azure hosting  (severity: info)
**Pattern:** A project references `Microsoft.Extensions.Azure` or `Azure.AI.OpenAI` and uses a secret-based credential (`ClientSecretCredential`, `EnvironmentCredential`) without a comment / config explaining why MSI was rejected.
**Why:** Secret rotation is operational toil and a credential-leak risk. Azure-hosted services should default to managed identity.
**Fix:** `new ManagedIdentityCredential(clientId)`.

## Output format

The scanner produces a markdown report:

```
## MAF anti-pattern scan — <repoPath>

### ❌ <N> errors
| Rule | File | Line | Match |
| MAF-AP-SEC-001 | src/Auth.cs | 42 | new DefaultAzureCredential() |
...

### ⚠️ <N> warnings
...

### ℹ️ <N> info
...

### ✅ <N> rules passed
```

## How the MCP tool invokes this

The `MafScanAntiPatterns(repoPath)` tool walks all `*.cs` files under `repoPath`, applies the regex / Roslyn rules defined in this skill, and produces the report above. The rule IDs in this document are the source of truth; the tool's internal rule list mirrors them.

## Updating this skill

When the team discovers a new anti-pattern that should be caught at scan time:

1. Add a new `MAF-AP-<AREA>-<NNN>` entry above with rule + pattern + fix + source.
2. Add the matching rule constant in `src/maf-autopilot/Tools/AntiPatternScannerTool.cs`.
3. Add a test in `src/maf-autopilot.Tests/AntiPatternScannerToolTests.cs` proving the new rule fires.
4. Cross-reference from `maf-constraints.instructions.md` if it represents a hard rule.

The cross-reference matters: if a constraint says "NEVER do X" but the scanner doesn't enforce it, the constraint is aspirational, not actionable.
