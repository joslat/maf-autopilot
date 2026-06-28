# SK→MAF Migration — Architecture & Principles Review Analysis

> **Scope:** the Semantic Kernel → Microsoft Agent Framework cross-framework migration feature shipped in **v1.5.0**.
> **Date:** 2026-06-27 · **Method:** 6 parallel reviewers (DRY · KISS/YAGNI · Clean Code · SOLID · Pragmatic architecture · Documentation), every finding **adversarially re-verified** by a second independent agent against the actual code/docs.
> **Prior hardening:** this feature already passed a 24-issue multi-agent review, a 7-principle assessment, an independent verification pass, and **10 rounds of GitHub Copilot review**. This analysis therefore targets *remaining* genuine violations and honestly separates them from deliberate, documented trade-offs.

---

## 1. Verdict

The feature is in **strong architectural health**. The review found:

- **0 high-severity violations. 0 correctness defects.**
- **2 medium** genuine violations (one DRY drift-guard gap, one stale reference doc).
- **6 low** genuine violations (maintainability / consistency / a doc over-claim).
- **~12 accepted trade-offs** — verified as deliberate and documented (no action, or action only when AutoGen lands).
- **4 false positives** — flagged by a reviewer, **rejected** on verification.

The pure-core / delivery-surface boundary is clean, dependencies point inward, and the toolkit's drift-test discipline is followed almost everywhere. The one conspicuous gap is that the **SK→MAF construct mapping** — the feature's core domain knowledge — is hand-maintained on three surfaces with **no** drift guard, even though every *analogous* surface in this very feature is guarded.

### Scorecard

| Principle | Health | Genuine violations | Notable |
|---|---|---|---|
| **DRY** | 🟢 Good | 1 medium, 0 low¹ | Construct-mapping triad unguarded (DRY-001) |
| **KISS / YAGNI** | 🟢 Good | 2 low | Double-parse per scan; a no-op forwarder |
| **Clean Code** | 🟢 Good | 2 low | `AnalyzeSource` length + forward-ref; one dense parse line |
| **SOLID** | 🟢 Good | 0 (1 actionable note) | OCP ordering invariant should be documented |
| **Pragmatic architecture** | 🟢 Good | 1 low | Resources catalogue lacks a drift test |
| **Documentation** | 🟡 Fair | 1 low + 1 medium | Stale `architecture.md`; sample README over-claim |

¹ DRY-002/003 are accepted trade-offs; DRY-004 is a false positive.

---

## 2. Genuine violations (act on these)

### 🟠 DRY-001 — Construct mapping authored in three places, no drift guard *(medium)*
**Where:** `SemanticKernelDetectorTool.cs:465-506` (`KnownTypes`) + `327-362` (`Classify`) · `docs/migration/from-semantic-kernel-to-maf.md:15-34,94-105` · `.github/skills/maf-from-semantic-kernel/SKILL.md:28-48`

The triad **(detectable SK type → bridge/rewrite/re-architect strategy → one-line fix note)** is hand-maintained on three surfaces read by three audiences (the scanner, the human guide, the navigator skill). The notes echo near-verbatim (e.g. the ChatHistory/`SerializeSession` note appears at code:495, guide:99, SKILL:41). **No test couples them** — and yet this feature *does* guard every analogous surface: `AllowedSkillNames_MatchSkillFolders_NoDrift`, `GetSkill_DescriptionLists_EveryAllowlistedName`, the three TourTool `*_NoDrift` catalogue tests, and the single-authority `MigrationSources`. The guide's own header *and* the `maf-migrate-from` prompt declare the guide *"the source of truth for the port,"* while `KnownTypes` carries an independent copy.

**Why it matters:** when the upstream Learn guide flips a strategy or renames a fix API, an editor updates the markdown a maintainer reads and silently leaves `KnownTypes` (or vice-versa); the tool then emits a plan that contradicts the guide the agent was told is authoritative. The copies agree *today* (spot-checked across planners, threads, filters, OTel, group-chat, templates, vector stores) — so it's a latent hazard, not a shipped bug, hence medium.

**Fix:** add one drift property-test (cheapest, in the existing house style) that parses the SKILL/guide tables and round-trips each type + strategy tag against `KnownTypes`/`Classify`. Fuller: make `KnownTypes` the machine source and generate the doc rows from it (as `maf://rules` is generated from `AllRules`).

### 🟠 DOCS-002 — `architecture.md` carries stale pre-1.5.0 counts + wrong cron cadence *(medium)*
**Where:** `docs/architecture.md:15,18,56,58,233` (+ stale banner line 3; test numbers 49/168; `docs/index.md:5`)

The canonical "how the pieces fit" doc's intro/component-map give wrong surface counts: **25 tools** (actual **27**), **12 skills / 6 agents** and **13/7** elsewhere (actual **14 skills / 8 agents**), and labels the watcher **"daily cron"** when the schedule is `0 6 * * 4` = **weekly Thursday**. The doc *contradicts itself* — its own body (lines 88-93) already has the correct 27/7/10. Root cause: a stale *"Phase S / 2026-05-13"* banner; the doc predates 1.5.0.

**Fix:** update the intro/map counts to 27 tools / 14 skills / 8 agents, "daily"→"weekly Thursday", refresh the test numbers toward the 967 in the CHANGELOG, and bump both stale banners.

> *Verifier correction:* two sub-claims in the original finding were **stripped** — the `463 vs 474` test numbers are **not** a contradiction (463 server + 11 analyzer = 474; both are merely stale), and the claim that the CHANGELOG calls `AddOpenAIChatCompletion` an invented API was a **misread** (it's the corrected, real form). See §4.

### 🟡 KISS-001 — Every `.cs` file is Roslyn-parsed twice per scan *(low)*
**Where:** `SemanticKernelDetectorTool.cs:129` (`HasGlobalSemanticKernelUsing` → parse at `:413`) then `:133` (`AnalyzeSource` re-parses at `:251`)

For the common case (no `global using Microsoft.SemanticKernel;`), the global-using probe parses every file to conclude "no," then `AnalyzeSource` parses each file again — a clean ~2× on the hot path with zero behavioural benefit. **Fix:** parse once in `Scan` into `(rel, text, root)`, add an `AnalyzeSource(SyntaxNode root, …)` overload, and keep the string overload as a one-line wrapper so the ~14 string-based tests are untouched.

### 🟡 KISS-002 — `MigrateScanCli.IsSupportedSource` is a no-op forwarder *(low)*
**Where:** `MigrateScanCli.cs:50` (`=> MigrationSources.IsSupported(source);`)

Three names for one yes/no question (`IsSupportedSource` → `IsSupported` → `Canonicalize`), used **only** by the CLI — the prompt and resource call `MigrationSources` directly. It carries its own xmldoc + a dedicated test re-asserting behaviour `MigrationSources` already owns. **Fix:** inline it at `Program.cs:143` (matching the other two surfaces) and delete the wrapper + test — *or* keep it deliberately as the CLI's unit-test seam (the sibling `DoctorCli`/`AutoFixCli` pattern) and acknowledge it forwards.

### 🟡 CLEAN-001 — `AnalyzeSource` is a 137-line method with a forward-referenced classifier *(low)*
**Where:** `SemanticKernelDetectorTool.cs:248-384`

The detector's semantic heart — and the method most edited when a construct is added — mixes gating + a dispatch switch + four nested local functions, and its key classifier `Classify` is **invoked at 290/293 but defined at 327** (legal only via local-function hoisting), itself a 5-branch waterfall hiding as a closure. **Fix:** promote `Classify` and `IsTypeReference` (already `static`/pure at 369) to private static methods — the exact pattern already used 5× in this file (`SimpleAttrName`, `ImportsSemanticKernel`, `IsKernelReceiver`, `RootIdentifierName`). `Record`/`LineOf` correctly stay local (they close over `hits`).

### 🟡 CLEAN-002 — `--source` parse line packs three things + a hidden side-effect *(low)*
**Where:** `MigrateScanCli.cs:34`

`if (a == "--source") { if (i + 1 < args.Length) source = args[++i]; continue; }` — a nested guard, a `continue`, and `args[++i]` which both reads *and* advances the loop counter, all on one line. In arg-parsing the class doc itself attributes to "the two parsing bugs the review found," the token-consumption invariant should be visible. **Fix:** split read from advance — `{ source = args[i + 1]; i++; }`.

### 🟡 PRAG-001 — `ResourceCatalogue` is the only TourTool catalogue with no drift test *(low)*
**Where:** `TourTool.cs:144-153` (resources; `maf://migrate-from` row at 151; free-floating `"14 skills available"` literal at 152) vs the tool/agent/prompt `*_NoDrift` tests at `TourToolTests.cs:97/124/159`

Three of four discovery catalogues are mechanically guarded; **resources — the one capability class this feature changed** — are not. A future rename of `maf://migrate-from` would leave a stale row with green CI. **Fix:** add `Catalogue_ListsEveryMcpServerResource_NoDrift` (prefix-normalising the `{?key}` template form against the catalogue's `?key=value` example form — see PRAG-TO-2 in §3), and assert the `"14"` literal equals `MafResources.AllowedSkillNames.Count`.

### 🟡 DOCS-001 — `sk-sample` README claims `KernelArguments` is detected; it isn't *(low)*
**Where:** `samples/sk-sample/README.md:14`

The fixture's only `KernelArguments` occurrence (`ClaimAgent.cs:39`) is an object-initializer LHS, deliberately excluded by `IsTypeReference`. The smoke test pins exactly **8** rewrite records *without* it. The showcase README documents output the detector provably never emits. **Fix:** drop `KernelArguments` from the bullet (the other constructs already account for the asserted 8).

---

## 3. Accepted trade-offs (verified deliberate — no action, or action when AutoGen lands)

These were each examined and confirmed as conscious, documented decisions — recorded here so a future reviewer doesn't re-litigate them.

| ID | Principle | Disposition |
|---|---|---|
| **DRY-002** — `"json"/"markdown"` token pair in two layers | DRY | Forced by the MCP string-`format` param; bounded. Tests pin the tool side. |
| **DRY-003** — repo-root walk-up copied in 7 test files | DRY | Pre-existing test-fixture dup; drift = flaky test, never a shipped bug. |
| **KISS-TO-1 / SOLID-DIP** — static `File`/`Directory`/`XDocument` IO | DIP | Pure IO-free core (`AnalyzeSource`/`SkScanResult`) already delivers testability; a per-tool `IFileSystem` seam would be inconsistent over-engineering. |
| **KISS-TO-2 / CLEAN-TO** — `ToMarkdown`/`ToJson`/`Verdict` on the record; `ToJson` returns anon object | KISS / Clean | Both formats required by the tool surface; self-rendering records are the house style; shape pinned by a reconciliation test. |
| **CLEAN-TO-2** — `*PromptExecutionSettings`/`*AgentThread`/`*Attribute` suffix literals | Clean | Single matcher site each, mirrored in the adjacent label, heavily commented; lifting to consts relocates rather than removes. |
| **KISS-FP / CLEAN** — `RootIdentifierName` chain-walker | KISS | The *minimum* test-pinned machinery that stops the name heuristic misfiring; confined to one helper. |
| **SOLID-OCP** — classification across switch + `Classify` if-ladder + `KnownTypes` | OCP | Genuinely heterogeneous syntax keys; **one actionable note:** document the required ordering on `Classify` (filters → `*PromptExecutionSettings` → `KnownTypes` → `*AgentThread` fallback) so the precedence invariant is explicit. |
| **SOLID-SRP** — csproj/CPM parsing on the detector | SRP | ~95 self-contained lines; extract a shared `CsprojPackageReader` **when a second package-prefix consumer (AutoGen) lands**. |
| **SOLID-ISP** — `MigrationStrategy`/`SkPackage`/`SkConstruct` are public | ISP | `SkConstruct`/`MigrationStrategy` are transitively public via `public AnalyzeSource(...)` (a deliberate pure-core entry point). `SkPackage` *is* a clean immediate `internal` demote (see remediation). |
| **YAGNI / source-seam** — `--source` plumbing for one value; rejection in 3 surfaces | YAGNI | Documented forward-compatible **input validation** (not a multi-source contract); the engine is correctly *not* abstracted; fail-loud is a present-day benefit. |
| **PRAG-TO-1** — CLI calls the `[McpServerTool]` method, not a pure core | Layering | Real low-severity smell, but `AutoFixTool`'s CLI does the same, and `TryNormalizeToDirectory` (`.csproj`/`.sln`→dir) lives only in the MCP method — so routing through it avoids duplication. A thin shared CLI seam is a nice-to-have. |
| **PRAG-TO-2** — catalogue shows `?source=…`, template is `{?source}` | Discovery | Intentional UX (concrete example reads better); it's the constraint the PRAG-001 drift test must prefix-normalise around. |

---

## 4. False positives (rejected on verification)

- **DRY-004** — sk-sample README vs smoke test "duplication": the legitimate machine-vs-human split (the test re-derives counts from the fixture each run; the README narrates per-file constructs for a reader). Different granularities, one self-guarding fixture.
- **KISS — `RootIdentifierName` over-engineered:** rejected — it's the minimum machinery (see §3).
- **KISS — `MigrateFrom` prompt "~130 lines":** rejected — the actual `AppendLine` body is **~30 lines** of linear prose, no control flow; the figure was overstated.
- **DOCS — "CHANGELOG calls `AddOpenAIChatCompletion` invented":** **misread.** `CHANGELOG.md:26` shows the *corrected*, real API form (matching the guide:125 and the fixture) — it does not assert it's invented.

---

## 5. Architecture summary

**What's genuinely strong (verified):**
- A real **pure core / delivery-surface boundary** — `Scan`/`AnalyzeSource`/`SkScanResult` are IO-free and MCP-free; the `[McpServerTool]` method, CLI, resource and prompt are thin adapters. Dependencies point inward.
- **Single-authority** `MigrationSources` consumed by CLI + resource + prompt (no allow-list drift).
- **Drift-test discipline** across tools, prompts, agents, and the skill allowlist — the toolkit's defining quality habit, followed here (with the one resource-catalogue gap, PRAG-001).
- **Honest trade-off documentation** — the syntactic-detection limits (name-based receiver, intra-file dedup, no `SemanticModel`, un-abstracted source dimension) are disclosed in code comments and *not* over-claimed in the docs. The guide's "Detected?" column is accurate row-by-row.

**The one structural theme worth internalising:** the feature guards every *meta* surface (catalogues, allowlists, counts) but not its *domain* surface (the construct mapping). DRY-001 + PRAG-001 are the same instinct applied one notch deeper.

See the companion **[Remediation Plan](./sk-migration-remediation-plan.md)** for prioritised, sized actions.
