# MAF 1.2.0 FraudClaimsTriage Sample — version-parity fixture

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║   ⚠️   WARNING — INTENTIONAL ANTI-PATTERN FIXTURE   ⚠️                       ║
║                                                                              ║
║   This codebase is a DELIBERATE collection of MAF anti-patterns, pinned to   ║
║   MAF 1.2.0 (the version BEFORE the 1.3 obsoletion wave). Every "broken"     ║
║   pattern is on purpose. Do NOT copy any of this code into a real project    ║
║   — it's bait for the maf-autopilot toolkit to find and fix.                 ║
║                                                                              ║
║   This sample exists alongside maf-1.3-sample/ and maf-1.0-sample/ as a      ║
║   version-parity fixture: same anti-patterns, different MAF pin, so the      ║
║   toolkit's detection is validated across the full supported version range.  ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

A standalone Phase W.A sample (companion to `samples/maf-1.3-sample/` and
`samples/maf-1.0-sample/`). Triple-version sample coverage lets W.9's CI
regression test pin workshop invariants on the full MAF NuGet history.

**Wait, didn't the registry say these patterns were "removed in 1.3"?** Yes —
and authoring this sample revealed that the registry's "removed in 1.3" labels
don't match the actual MAF NuGet surface. **See the [Phase W.A finding](#phase-wa-finding--maf-1213-public-surface-is-essentially-the-same)
below.**

## Topology

```
IntakeAgent (ChatClientAgent + Azure OpenAI)
    │
    ├── (fan-out) ──► Osint / History / Transactions investigators
    │                       └─► (fan-in barrier) ──► Aggregator
    │                                                    │
    │                                                    ▼
    │                                              DecisionAgent
    │                                                    │
    │                                                    ▼
    │                                              Notification
```

Same shape as the 1.3 sample. The only API difference: **MAF 1.2.0's `Executor`
abstract class requires overriding `ConfigureProtocol(ProtocolBuilder)`** —
the 1.3 source generator removed that requirement. Every executor in this
sample provides a no-op stub for it.

## Embedded anti-patterns (10 errors + 3 warnings + 3 silent-starvation)

Identical to the 1.3 sample — the toolkit reports **grade F** with the same
counts. See `samples/maf-1.3-sample/README.md` for the full per-rule table.

## How to run / audit

```bash
# Build
dotnet build samples/maf-1.2-sample/MafSample.FraudClaims12.csproj

# Audit (from repo root)
dotnet run --project src/maf-autopilot --framework net10.0 -- doctor samples/maf-1.2-sample
# → 🔴 grade F — 10 anti-pattern errors + 3 silent-starvation risks
```

## Phase W.A finding — MAF 1.2/1.3 public surface is essentially the same

While authoring this sample (2026-05-13), `dotnet-inspect` recon against
`Microsoft.Agents.AI@1.2.0` revealed two registry / compatibility-matrix
inaccuracies worth noting:

1. **The "removed in 1.3" pattern claims are misleading.** The registry's
   `MAF130-THREAD-001`, `MAF130-A2A-001/002`, `MAF130-STREAM-001`,
   `MAF130-EVENT-001`, `MAF130-ATTR-001/002` all describe types and methods
   that do NOT appear in MAF 1.2.0's public surface either. They must have
   been removed BEFORE 1.2.0 (likely pre-1.0 or from the removed
   `Microsoft.Agents.AI.DevUI` + `Microsoft.Agents.AI.Hosting` packages).
   The labels imply a 1.2 → 1.3 obsoletion event but in reality the
   transition happened earlier. **Documented as Phase T.7 registry
   chronology gap in next-steps.md.**

2. **The compatibility-matrix's minimum-version pins for 1.2.0 are wrong.**
   It listed `Microsoft.Extensions.AI >= 10.3.0` and `Azure.AI.OpenAI >= 2.6.0`.
   Real minimums (verified via NuGet restore against
   `Microsoft.Agents.AI 1.2.0`):
   - `Microsoft.Extensions.AI 10.5.0` (transitive pin on MAF 1.2.0 itself)
   - `Azure.AI.OpenAI 2.8.0-beta.1` (2.7.0 was never released; 2.6.0 stable
     not on nuget.org)

   The compat-matrix entries describe "minimum versions that worked when the
   sample was written" rather than "what MAF 1.2.0 actually transitively
   requires."

## Provenance

Cloned from `samples/maf-1.3-sample/` with three diffs:
1. csproj pin to MAF 1.2.0 + the real-minimum MEAI/Azure.AI.OpenAI versions.
2. AssemblyName disambiguated (`MafSample.FraudClaims12`) to avoid filesystem
   collision when both samples are built on the same machine.
3. Each Executor class adds `protected override ProtocolBuilder
   ConfigureProtocol(ProtocolBuilder builder) => builder;` — required by MAF
   1.2.0's `Executor` abstract base, removed in 1.3.0.

License: MIT. Not intended for production use.
