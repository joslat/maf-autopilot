# SK→MAF Migration — Remediation Plan

> Companion to the **[Architecture & Principles Review](./sk-migration-architecture-and-principles-review.md)** (2026-06-27).
> Every item below is a **verified genuine violation** or a **verified actionable note**. Accepted trade-offs and false positives are intentionally excluded (see the review §3–§4).
> **Nothing here is a correctness or security defect.** This is maintainability, drift-resistance, and doc-accuracy hardening.

Effort key: **XS** ≈ minutes · **S** ≈ <1h · **M** ≈ a few hours. Risk = chance of regressing behaviour.

---

## Tier 1 execution tracker

> Branch: `chore/sk-migration-review-tier1` · Started 2026-06-27. Status legend: ⬜ pending · 🔄 in progress · ✅ done.
>
> **✅ Tier 1 complete (2026-06-27).** All 6 items implemented. +3 drift tests (construct-mapping ↔ skill table; resource catalogue; skills-count). The DRY-001 guard was proven by mutation (flipping one strategy tag fails CI with a precise message). **978 server tests green across net8/9/10** (993 total incl. analyzer).

| # | Item | Finding | Effort | Status |
|---|---|---|---|---|
| 1 | Fix `architecture.md` stale counts + cron + banner (+ `docs/index.md` banner) | DOCS-002 | S | ✅ done |
| 2 | Drop `KernelArguments` from `sk-sample/README.md` | DOCS-001 | XS | ✅ done |
| 3 | Add construct-mapping drift test (`KnownTypes` ↔ guide/SKILL tables) | DRY-001 | M | ✅ done |
| 4 | Add resource-catalogue drift test + pin the "14 skills" literal | PRAG-001 | S | ✅ done |
| 5 | Document the required ordering on `Classify` | SOLID-OCP note | XS | ✅ done |
| 6 | Split the `args[++i]` parse line | CLEAN-002 | XS | ✅ done |

---

## Tier 1 — Do now (cheap; closes accuracy & drift gaps)

These are low-risk, high-trust-per-hour. Total ≈ half a day.

| # | Item | Finding | Effort | Risk |
|---|---|---|---|---|
| 1 | Fix `architecture.md` stale counts + cron + banner | DOCS-002 (med) | S | none (docs) |
| 2 | Drop `KernelArguments` from `sk-sample/README.md` | DOCS-001 (low) | XS | none (docs) |
| 3 | Add construct-mapping drift test (`KnownTypes` ↔ guide/SKILL tables) | DRY-001 (med) | M | low |
| 4 | Add resource-catalogue drift test + pin the "14 skills" literal | PRAG-001 (low) | S | low |
| 5 | Document the required ordering on `Classify` | SOLID-OCP note | XS | none |
| 6 | Split the `args[++i]` parse line | CLEAN-002 (low) | XS | none (test-pinned) |

### 1 · `architecture.md` accuracy
- **Do:** in `docs/architecture.md` set tools `25→27`, skills `12/13→14`, agents `6/7→8` (lines 15/18/56/58); `"daily cron"→"weekly Thursday cron"` (line 233); refresh the stale test numbers toward `967` (lines 49/168); bump the `Phase S / 2026-05-13` banner (line 3) **and** `docs/index.md:5`.
- **Don't:** touch the `463/474` reasoning as if it were a contradiction (it reconciles: 463 server + 11 analyzer), and **don't** "fix" `CHANGELOG.md:26` (the `AddOpenAIChatCompletion` example is the *correct* form — verified).
- **Accept when:** every count in `architecture.md` matches the README (27/7/10/14/8) and the body; cron reads "weekly Thursday".

### 2 · `sk-sample/README.md`
- **Do:** remove `KernelArguments` from the ClaimAgent rewrite bullet (`README.md:13-14`). The remaining constructs + the two `ChatCompletionAgent` occurrences already account for the smoke-tested **8** rewrite records.
- **Accept when:** the README's rewrite list matches the constructs the smoke test asserts (no construct the detector excludes).

### 3 · Construct-mapping drift test *(the one with teeth)*
- **Do:** add a test (in the existing `*_NoDrift` house style) that parses the `.github/skills/maf-from-semantic-kernel/SKILL.md` "Construct → MAF target → strategy" table (and/or the guide table), extracts each SK type + its 🌉/🔁/🏗 tag, and asserts it round-trips against `KnownTypes`/`Classify`. Tolerate the documented "no type signature" (⚠) rows.
- **Stretch (optional, larger):** make `KnownTypes` the machine source and *generate* the doc lookup rows from it (mirroring how `maf://rules` is generated from `AllRules`). Not required for Tier 1.
- **Accept when:** changing a strategy tag in either the code or the SKILL/guide table *fails CI* until the other is updated.

### 4 · Resource-catalogue drift test
- **Do:** add `Catalogue_ListsEveryMcpServerResource_NoDrift` reflecting over `[McpServerResource]` `UriTemplate`s, asserting each declared template has a matching `ResourceCatalogue` row. **Prefix-normalise**: strip from the first `{` or `?` so `maf://migrate-from{?source}` matches the catalogue's `maf://migrate-from?source=semantic-kernel` (and `maf://skills{?name}` ↔ `maf://skills?name=<X>`) — the catalogue's example form is the intended UX (PRAG-TO-2), so the *test* adapts, not the catalogue. Also assert `TourTool.cs:152`'s `"14 skills available"` count equals `MafResources.AllowedSkillNames.Count`.
- **Accept when:** renaming/removing any `[McpServerResource]` fails CI, and adding a 15th skill fails the count assertion.

### 5 · Document `Classify`'s ordering invariant
- **Do:** add an XML-doc note on `Classify` (`SemanticKernelDetectorTool.cs:327`) stating the **required order** — filters → `*PromptExecutionSettings` suffix → `KnownTypes` lookup → `*AgentThread` suffix fallback — and *why* (`AgentThread`/`ChatHistoryAgentThread` mis-resolve if reordered).
- **Accept when:** the precedence invariant is explicit in the doc-comment, not implied only by statement order.

### 6 · Split the dense parse line
- **Do:** `MigrateScanCli.cs:34` → `if (a == "--source") { if (i + 1 < args.Length) { source = args[i + 1]; i++; } continue; }`.
- **Accept when:** the read (`args[i+1]`) and the loop-advance (`i++`) are separate statements; `Parse_SourceBeforePath_DoesNotEatThePath` + `Parse_SourceAsTrailingFlagWithNoValue_KeepsDefault` still pass.

---

## Tier 2 — Opportunistic (do when the file is next open)

Net-positive but not worth a dedicated PR; bundle with the next change to these files.

| # | Item | Finding | Effort | Risk |
|---|---|---|---|---|
| 7 | Promote `Classify` + `IsTypeReference` to private static methods | CLEAN-001 | S | low |
| 8 | Parse each file once; add `AnalyzeSource(SyntaxNode root,…)` overload | KISS-001 | S–M | low |
| 9 | Inline `IsSupportedSource` (or keep + acknowledge the CLI test seam) | KISS-002 | XS | low |
| 10 | Add a shared internal CLI seam (`RunCli`) both CLI + MCP method call | PRAG-TO-1 | S | low |
| 11 | Demote `SkPackage` to `internal` | SOLID-ISP | XS | low |

- **#7** — `IsTypeReference` is already `static`/pure (zero-friction); `Classify` closes only over `Record`, so pass a small `record` delegate (or accumulate on an instance). `AnalyzeSource` then collapses to *gate → flat switch (handlers defined beside it) → projection*.
- **#8** — keep the string-based `AnalyzeSource(source, fileName, repoEstablishesSkContext)` as a one-line wrapper so the ~14 string-based tests and the smoke tests are untouched.
- **#10** — `internal static string RunCli(path, bool json)` doing `TryNormalizeToDirectory + PathGuard + Scan + ToMarkdown/ToJson`, called by both `Program.cs:150` and `MafDetectSourceFramework` — mirrors `DoctorTool.Run`/`MafDoctor` both calling `RunCore`, and shares the currently-MCP-only `.csproj`/`.sln` path normalisation.
- **#11** — `SkPackage` flows only through the already-`internal` `Scan`/`DetectPackages`; a clean demote. (Do **not** demote `SkConstruct`/`MigrationStrategy` unless you also make `AnalyzeSource` internal — they're transitively public via the pure-core entry point.)

---

## Tier 3 — When the second source (AutoGen) lands

Deferring these is the *correct* call today (premature extraction for a single caller). The trigger is a second package-prefix / source consumer.

| # | Item | Finding | Effort |
|---|---|---|---|
| 12 | Extract a shared `CsprojPackageReader.ReadPackageReferences(repoPath, idPredicate)` + CPM resolver into the `Tools` layer (beside `SourceFileWalker`) | SOLID-SRP | M |
| 13 | Extract the `ISourceFrameworkDetector` seam; thread `source` through the tool method; fold a shared "not supported" message constant into `MigrationSources` | source-seam / PRAG | M–L |

- **#12** — `SemanticKernelDetectorTool` then calls it with the SK-prefix predicate; AutoGen reuses it with its own prefix instead of copy-paste.
- **#13** — at that point the three rejection bodies (CLI exit-2, prompt STOP, resource exception) share wording via the constant; the class note (`SemanticKernelDetectorTool.cs:34-39`) already anticipates this.

---

## Suggested sequencing

1. **Tier 1 in one PR** (docs + the two drift tests + the two micro-nits + the doc-comment). ~½ day, low risk, closes the only medium findings and the last drift gap. Recommend doing this.
2. **Tier 2 folded into future edits** of `SemanticKernelDetectorTool.cs` / `Program.cs` — no standalone PR needed.
3. **Tier 3 gated on AutoGen** — re-open this plan when the second source is scheduled.

> None of this blocks anything; v1.5.0 is correct and shipped. Tier 1 is the recommended next increment.
