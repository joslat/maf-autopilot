# maf-doctor output schemas

Tools that emit structured output document their schemas here. JSON outputs are stable within a major version (`schema_version` field tracks evolution).

## `MafDoctor`

Pass `format: "json"` to get machine-readable output.

```json
{
  "schema_version": "1",
  "verdict": "C",
  "errors_count": 1,
  "warnings_count": 0,
  "silent_starvation_risks": 0,
  "unbounded_cost_sites": 0,
  "top_fixes": [
    {
      "rule_id": "MAF-AP-SEC-003",
      "severity": "error",
      "file": "src/MyAgent.cs",
      "line": 42,
      "issue": "EnableSensitiveData = true",
      "fix_description": "Remove EnableSensitiveData = true from non-dev configurations.",
      "auto_fixable": true,
      "why": "Sensitive-data logging writes prompts and responses (often PII / secrets) to your telemetry sink — acceptable in dev, a data-leak in production.",
      "confidence": "high"
    }
  ],
  "summary_md": "...the full markdown report that format:markdown would emit..."
}
```

### Field definitions

- `schema_version`: `"1"` — increments only on breaking schema changes (new 1.x releases keep schema v1)
- `verdict`: `"A"` | `"B"` | `"C"` | `"F"` per the grading rubric in `DoctorTool` description
- `errors_count`: anti-pattern errors + prompt-lint errors (integer)
- `warnings_count`: anti-pattern warnings + prompt-lint warnings + info notes (integer)
- `silent_starvation_risks`: fan-out handler violations (integer)
- `unbounded_cost_sites`: agent call sites with no `MaxOutputTokens` cap (integer)
- The four counts above **reconcile with `top_fixes`** in full mode: `errors_count + warnings_count + silent_starvation_risks + unbounded_cost_sites == top_fixes.length`.
- `top_fixes`: array of finding objects — the top 3 by default, or **every** finding when `--all` / `full: true` is set (the key name is retained for schema-v1 stability)
  - `rule_id`: the canonical rule identifier (e.g., `MAF-AP-SEC-001`, `MAF001`, `COST-001`)
  - `severity`: `"error"` | `"warning"` | `"starvation_risk"` | `"cost"`
  - `file`: relative path to the file containing the finding (links directly to source)
  - `line`: line number within that file (1-based integer)
  - `issue`: short, human-readable title of the finding
  - `fix_description`: one-line actionable fix; `MafAutoFixAll` uses this as its rewrite spec
  - `auto_fixable`: `true` if `MafAutoFixAll` has a deterministic rewriter for this rule
  - `why`: one-line consequence / rationale for the finding. **Always present**; the empty string `""` when no rationale is defined for the rule.
  - `confidence`: detector trust level for **false-positive triage** — `"certain"` (compiler ground-truth) | `"high"` (structural AST, low FP) | `"heuristic"` (name-only / text / scope-limited — **verify before fixing**). Additive within schema v1; consumers that don't recognize it can ignore it. The `maf-remediate` prompt reads this to decide which findings to confirm before changing code.
- `summary_md`: the same markdown that `format: "markdown"` would emit (for hybrid consumers). It includes the offending source line per finding, so it inherits the markdown report's **best-effort, content-aware secret redaction** — the structured `top_fixes` array carries no source text and is the safer channel for machine consumers.

### Usage in CI

```bash
# Get JSON output and check verdict.
# CLI flag is `--json` (boolean); the MCP tool parameter is `format: "json"`.
OUTPUT=$(maf-doctor doctor . --json 2>/dev/null || \
  dotnet run --project src/maf-autopilot/ -- doctor . --json)

VERDICT=$(echo "$OUTPUT" | jq -r '.verdict')
if [ "$VERDICT" = "F" ]; then
  echo "MAF Doctor grade F — failing CI"
  exit 1
fi
```

Or via the MCP tool from your AI client (note: the MCP parameter is `format: "json"`, not the CLI's `--json` flag):
```
MafDoctor(repoPath: ".", format: "json")
```

---

## `MafDoctor` plan output (`--plan` / `format: "plan"`)

A **markdown-only** remediation plan (no JSON/SARIF schema) — an ordered, checkboxed
punch list you can paste into a GitHub issue:

- **Phase 1 — Quick wins:** every auto-fixable finding, batched into one `autofix-all`
  step, with the cleared rules listed. (Note: `autofix-all` also applies build-surfaced
  rewriters such as the fan-in arg-order swap (`MAF130-FAN-IN-001`) that the scan-time
  grader doesn't flag — so it may change more than the plan enumerates.)
- **Phase 2 — Semantic fixes:** the non-auto-fixable findings as impact-ordered `- [ ]`
  tasks (why / fix / `file:line`), tagged for the `@maf-migration` agent.

It **always covers every finding**, ignoring `--all` / `full`, and emits **no source
snippets** — only `file:line` — so it never echoes a secret.

---

## (Reserved) `MafAuditPullRequest`

Same shape as `MafDoctor` with an additional `pr_metadata` field. Implementation pending.

---

## `MafScanAntiPatterns` SARIF output

Documented separately — uses standard [SARIF v2.1.0](https://docs.oasis-open.org/sarif/sarif/v2.1.0/sarif-v2.1.0.html). Pass `format: "sarif"`.

## `MafValidateFanOut` SARIF output

Same standard SARIF v2.1.0. Pass `format: "sarif"`. Flags both `SilentStarvationRisk` and `LikelyInvalid` as `MAF001` errors.
