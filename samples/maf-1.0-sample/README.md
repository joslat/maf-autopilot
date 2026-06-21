# MAF 1.0.0 FraudClaimsTriage Sample — earliest version-parity fixture

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                                                                              ║
║   ⚠️   WARNING — INTENTIONAL ANTI-PATTERN FIXTURE   ⚠️                       ║
║                                                                              ║
║   This codebase is a DELIBERATE collection of MAF anti-patterns, pinned to   ║
║   MAF 1.0.0 (the EARLIEST stable release). Every "broken" pattern is on      ║
║   purpose. Do NOT copy any of this code into a real project — it's bait for  ║
║   the maf-doctor toolkit to find and fix.                                    ║
║                                                                              ║
║   This is the third in a 3-version sample set (1.0 / 1.2 / 1.3). The set     ║
║   pins the toolkit's detection across the full MAF NuGet history via W.9's   ║
║   CI regression test.                                                        ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

A standalone Phase W.A sample (companion to `samples/maf-1.3-sample/` and
`samples/maf-1.2-sample/`). The deepest version-pin in the set —
demonstrates that the toolkit's detection works all the way back to MAF's
initial public release.

## Topology

Same as the 1.2 and 1.3 samples. Like the 1.2 sample, the 1.0 `Executor`
abstract class requires overriding `ConfigureProtocol(ProtocolBuilder)`.

## Embedded anti-patterns (10 errors + 3 warnings + 3 silent-starvation)

Identical to the 1.3 sample. The toolkit reports **grade F** with the same
counts (validated by `samples/maf-1.0-sample/.expected-doctor.yaml` +
`src/maf-autopilot.Tests/SampleRegressionTests.cs`).

## How to run / audit

```bash
dotnet build samples/maf-1.0-sample/MafSample.FraudClaims10.csproj
dotnet run --project src/maf-autopilot --framework net10.0 -- doctor samples/maf-1.0-sample
# → 🔴 grade F
```

## Phase W.A findings — applied here too

Same compat-matrix corrections discovered for the 1.2 sample apply: the
real transitive pin from `Microsoft.Agents.AI 1.0.0` is also
`Microsoft.Extensions.AI 10.5.0` + `Azure.AI.OpenAI 2.8.0-beta.1`. The
compat-matrix's "MEAI >= 9.0.0" / "Azure >= 2.0.0" for 1.0.0 are stale.
See `samples/maf-1.2-sample/README.md` for the full chronology finding.

## Provenance

Cloned from `samples/maf-1.2-sample/` (same recipe + version pin shift to 1.0.0).
License: MIT. Not intended for production use.
