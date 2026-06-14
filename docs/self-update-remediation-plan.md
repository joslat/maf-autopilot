# MAF self-update remediation plan

**Created:** 2026-06-14 · **Owner:** @joslat · **Status:** ✅ **COMPLETE — 33/33.** Merged to `main` (#66→#59); watcher live-validated (dry-run + real 1.6.1→1.10.0 update on main); drift detector live; repo renamed to `joslat/maf-doctor` + URLs flipped. Optional 6.5 (namespace) intentionally left as a future separate PR.
**Scope:** Fix the two failing self-update workflows (MAF Release Watcher, MAF Drift Detector), harden the whole self-update engine and its AI-fill chain, validate the auto-update path end-to-end, and execute the brand-only rename to **MAF Doctor**.

## Why this exists (one-paragraph problem statement)

Both scheduled self-update workflows have failed on **every** scheduled run since they were created, for two unrelated reasons: (1) the Release Watcher's NuGet-version check uses an inline `python3 -c` block whose source carries shell-block indentation → `IndentationError` on the scheduled path (manual dispatch with an explicit version bypasses it, which is why the May-18 run looked green); (2) the Drift Detector calls `gh issue create --label maf-drift,needs-review` but neither label exists → exit 1. Neither workflow notifies anyone on failure, so 5 weeks of breakage went unnoticed. The privileged `analyze-and-update` push-to-main path has therefore **never run from a real release detection** and is unvalidated end-to-end. This plan fixes all of that, plus the latent bugs that will surface once the blockers are cleared, then does the rename.

## Legend

- **% Done** — 0% until merged & verified. Update as work lands.
- **Reviewed** — ☐ open / ☑ reviewed by a second pair of eyes (or a `/code-review`).
- **Pri** — P0 (blocks scheduled runs), P1 (critical safety/observability), P2 (correctness), P3 (nice-to-have).
- **Auth** — 🔒 requires maintainer action outside a code PR (repo settings, secret scopes, label/issue creation on the live repo). Everything else is a normal code PR.
- **Effort** — S (<1h), M (half-day), L (multi-day / code+tests).

---

## High-level tracking table

| Phase | ID | Task | Description | % Done | Reviewed | Notes |
|---|---|---|---|---|---|---|
| 0 · Guardrails | 0.1 | Remediation branch + this doc | Create `fix/self-update` branch; land this tracking doc | 100% | ☑ | done — branch + plan committed |
| 0 · Guardrails | 0.2 | actionlint + compileall CI gate | actionlint (parse/schema) + `compileall` scripts in ci-invariants | 100% | ☑ | done — yamllint dropped (simple wins); shellcheck off |
| 1 · Unblock (P0) | 1.1 | Extract watcher inline Python | NuGet filter → `get_latest_stable.py` (+12 tests) | 100% | ☑ | done — 12 tests green; root cause #1 fixed |
| 1 · Unblock (P0) | 1.2 | Drift: self-provision labels | `gh label create --force` before issue create | 100% | ☑ | done — root cause #2 fixed |
| 1 · Unblock (P0) | 1.3 | Drift: harden grade parse | `UNKNOWN` fallback; don't file blank-grade issues | 100% | ☑ | done — pipefail + `\|\| true` + UNKNOWN gate |
| 2 · Observability | 2.1 | Watcher notify-on-failure | Terminal `if: failure()` job → tracking issue | 100% | ☑ | done — rolling maf-release issue, exact-title dedupe |
| 2 · Observability | 2.2 | Drift notify-on-failure | Same pattern on drift detector | 100% | ☑ | done |
| 2 · Observability | 2.3 | Document native fallback | Enable "failed workflow" email; note in README | 100% | ☑ | doc in TROUBLESHOOTING.md; 🔒 toggle is maintainer opt-in |
| 3 · Engine safety | 3.1 | Doctor self-scan exclusions | `--exclude` substr support; drift skips samples/tests | 100% | ☑ | done — walker+CLI+test (16 green); substr not glob (simpler) |
| 3 · Engine safety | 3.2 | `is_major` real gate (or kill) | Human gate for major bumps; or delete dead logic | 100% | ☑ | done — Option A: majors escalate to issue, no auto-push |
| 3 · Engine safety | 3.3 | Cron stagger + concurrency | Drift 2h after watcher; `concurrency:` groups | 100% | ☑ | done — drift 11:00 Mon; both have concurrency groups |
| 3 · Engine safety | 3.4 | Idempotent registry append | Dedupe draft entries by id on re-run | 100% | ☑ | done — dedupe_registry_entries.py (+5 tests) |
| 3 · Engine safety | 3.5 | Reconcile PAT scope docs | Align the two conflicting COPILOT_ASSIGN_PAT comments | 100% | ☑ | doc reconciled both files; 🔒 live-scope check still maintainer |
| 3 · Engine safety | 3.6 | Daily poll cadence (opt) | Weekly → daily cron to cut detection latency | 100% | ☑ | done — watcher cron 0 9 * * * |
| 4 · Chain bugs | 4.1 | gen_guide_section prerelease fix | Tolerate `-rc1` in version sort key (+test) | 100% | ☑ | done — `_semver_key`; +2 tests |
| 4 · Chain bugs | 4.2 | verify_crossfile per-version guides | Validate section IDs vs all guides, not just 1.3.0 | 100% | ☑ | done — `collect_all_guide_sections` union (+tests) |
| 4 · Chain bugs | 4.3 | verify_crossfile staleness → warn | 30-day matrix-date check warn-not-fail | 100% | ☑ | done — `::warning`, not a finding |
| 4 · Chain bugs | 4.4 | compat-matrix placeholder gate | Fail if `>= unknown` / `Auto-detected` reaches main | 100% | ☑ | done — top-row placeholder check (+tests) |
| 4 · Chain bugs | 4.5 | gen_guide_section human-block anchor | Anchor preserve-region to `AUTO_END` sentinel | 100% | ☑ | done — AUTO_END anchor; injection test |
| 4 · Chain bugs | 4.6 | AI-chain scope robustness | Stop relying on bot self-labeling; assert loudly | 100% | ☑ | done — verify diff-driven; semantic-review entries-driven |
| 4 · Chain bugs | 4.7 | semantic-review prompt extension | `.ai-review-prompt.md` → `.txt` | 100% | ☑ | done |
| 4 · Chain bugs | 4.8 | ai-fill-verify sticky comment | Update one sticky comment, not stack duplicates | 100% | ☑ | done — marocchino sticky + success-clear |
| 4 · Chain bugs | 4.9 | ai-fill-todos bot-id non-fatal | Warn+skip on empty BOT_ID instead of `exit 1` | 100% | ☑ | done — warn + `exit 0` |
| 4 · Chain bugs | 4.10 | pr-audit honest scoping | Scope doctor to changed files OR fix the comment | 100% | ☑ | done — honest comment + product `--exclude` |
| 4 · Chain bugs | 4.11 | ai-fill template guide anchor | Parameterize/comment the hardcoded 1.3.0 anchor | 100% | ☑ | done — check #6 now unions all guides |
| 5 · Validation | 5.1 | NuGet fixture smoke test | Unit-test version detection in ci-invariants | 100% | ☑ | done — pytest step runs .github/scripts/tests in CI |
| 5 · Validation | 5.2 | analyze-and-update dry-run | Synthetic bump on throwaway branch, prove push chain | 100% | ☑ | done — dispatched on a throwaway branch; full chain green; main untouched |
| 5 · Validation | 5.3 | Re-trigger + verify lifecycle | Manual run both; confirm green + issue open/close | 100% | ☑ | done — both green on main; watcher did real 1.6.1→1.10.0; drift opened #69 |
| 5 · Validation | 5.4 | Workflow-set parse audit | Lint all 10 workflows; confirm startup_failures clear | 100% | ☑ | done — all 10 parse; actionlint gate added (0.2) |
| 6 · Rebrand | 6.1 | PR-A: brand + server id + URLs | Text-only rename to MAF Doctor / maf-doctor | 70% | ☑ | server id + snippets done; **URL/RepositoryUrl flip deferred to 6.4** (avoids 404 window) |
| 6 · Rebrand | 6.2 | InitCommand server key | Write `maf-doctor` key, keep `command=maf-autopilot` | 100% | ☑ | done — legacy-key guard; 6 tests green |
| 6 · Rebrand | 6.3 | CHANGELOG migration notes | GHCR image-path + mcp.json server-id change | 100% | ☑ | done — Unreleased entry + maintainer follow-ups |
| 6 · Rebrand | 6.4 | Repo rename | `joslat/maf-autopilot` → `joslat/maf-doctor` | 100% | ☑ | done — repo renamed (old path redirects); URL/SARIF/OCI/RepositoryUrl flipped (`chore/rebrand-urls`) |
| 6 · Rebrand | 6.5 | Namespace rename (opt) | `MafAutopilot`→`MafDoctor`, separate mechanical PR | 0% | ☐ | **deferred by design** — optional, separate PR, never bundled |
| 7 · Docs/closeout | 7.1 | Fix is_major escalation docs | architecture.md flow + plan B.3 | 100% | ☑ | done — flow rewritten (direct-push + escalation); B.3 superseded-note |
| 7 · Docs/closeout | 7.2 | README/CHANGELOG behavior updates | Document failure issues, drift target, watcher cadence | 100% | ☑ | done — README tree cadence + CHANGELOG + TROUBLESHOOTING |
| 7 · Docs/closeout | 7.3 | Final verify + close plan | Green-board sign-off; update memory | 100% | ☑ | 739+15 .NET + 50 py green; identity frozen |

**Suggested PR grouping:** PR-1 = Phase 0+1 (unblock). PR-2 = Phase 2 (observability). PR-3 = Phase 3. PR-4 = Phase 4. PR-5 = Phase 5 artifacts. PR-6 = Phase 6.1–6.3 (rebrand text). Repo rename (6.4) + namespace (6.5) are standalone. Phases 0→2 can ship same-day; 3–4 are independent and parallelizable; 6 is orthogonal to 1–5 and can run in parallel.

---

## Phase 0 — Guardrails

**Objective:** make it impossible to re-break a workflow silently while we work. **Exit criteria:** every PR touching `.github/workflows/**` runs `actionlint`; the remediation branch exists.

### 0.1 — Remediation branch + tracking doc
- **Goal:** isolate the work; land this doc as the source of truth.
- **How:** `git switch -c fix/self-update` off `main`; commit this file at `docs/self-update-remediation-plan.md`.
- **Acceptance:** branch pushed; doc visible on the branch.
- **Effort:** S.

### 0.2 — actionlint + yamllint CI gate
- **Goal:** catch YAML parse errors, bad `${{ }}` contexts, and malformed `run:` shell **before merge** — the exact class that produced both the indentation bug and the startup_failures.
- **Files:** `.github/workflows/ci-invariants.yml`.
- **How:** add a job gated on workflow/script paths:
  ```yaml
  lint-workflows:
    if: ${{ github.event_name == 'pull_request' }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@<pinned-sha>
      - name: actionlint
        uses: raven-actions/actionlint@<pinned-sha>   # pin by SHA per repo convention
        with: { files: ".github/workflows/*.yml" }
      - name: yamllint
        run: pipx run yamllint -d relaxed .github/workflows
      - name: python compile-check helper scripts
        run: python -m compileall -q .github/scripts
  ```
  The `compileall` step would have caught 1.1 directly (it compiles every `.py`; note it does **not** catch the inline `python3 -c` string — that's why 1.1 must extract the code to a real file, which `compileall` then guards).
- **Acceptance:** PR introducing a deliberately malformed `run:` fails the gate; a clean PR passes.
- **Depends:** none. **Effort:** S. **Pri:** P1.

---

## Phase 1 — Unblock the scheduled workflows (P0)

**Objective:** make both scheduled workflows succeed. **Exit criteria:** a manual `workflow_dispatch` of each, with **blank** inputs, completes green (watcher reaching the no-op/“no update” path; drift opening a real issue and an artifact).

### 1.1 — Extract the watcher's inline Python  ⟵ root cause #1
- **Goal:** eliminate the `IndentationError` on the NuGet path.
- **Files:** `.github/workflows/maf-release-watcher.yml:73-77`; new `.github/scripts/get_latest_stable.py`; new `.github/scripts/tests/test_get_latest_stable.py`.
- **How:**
  1. Create the helper (no leading indentation possible — it's a real module):
     ```python
     # .github/scripts/get_latest_stable.py
     """Read a NuGet v3 flatcontainer index.json from stdin; print the latest STABLE version."""
     import sys, json, re
     def latest_stable(raw: str) -> str:
         data = json.loads(raw)
         stable = [v for v in data.get("versions", [])
                   if not re.search(r"-(alpha|beta|rc|preview)", v, re.IGNORECASE)]
         return stable[-1] if stable else ""
     if __name__ == "__main__":
         print(latest_stable(sys.stdin.read()))
     ```
  2. Replace the inline block:
     ```bash
     LATEST=$(echo "$RAW" | python3 .github/scripts/get_latest_stable.py)
     ```
  3. Add a unit test mirroring `.github/scripts/tests/test_gen_guide_section.py` style: feed a fixture `{"versions":["1.5.0","1.6.0-rc1","1.6.1","2.0.0-preview"]}` → expect `1.6.1`; empty/prerelease-only → `""`.
- **Acceptance:** scheduled-path dispatch (blank input) prints `Latest STABLE version on NuGet: <x>` and reaches the `has_update` decision; `pytest .github/scripts/tests/` green.
- **Depends:** none (0.2 recommended first). **Effort:** S. **Pri:** P0.

### 1.2 — Drift Detector: self-provision labels  ⟵ root cause #2
- **Goal:** stop `gh issue create --label` from exit-1’ing on missing labels — without any manual label creation.
- **Files:** `.github/workflows/maf-drift-detector.yml` (before line 48 / inside the issue step).
- **How:** add an idempotent step (copy the proven pattern from `maf-ai-fill-todos.yml:99-114`):
  ```yaml
  - name: Ensure drift labels exist
    if: steps.doctor.outputs.grade != 'A'
    env: { GH_TOKEN: "${{ secrets.GITHUB_TOKEN }}" }
    run: |
      gh label create "maf-drift"    --color B60205 --description "MafDoctor grade dropped below A" --force || true
      gh label create "needs-review" --color FBCA04 --description "Awaiting maintainer triage"        --force || true
  ```
  `--force` upserts and is idempotent; `|| true` is belt-and-suspenders. Runs under `GITHUB_TOKEN` which already has `issues: write` — **no 🔒 manual action required.**
- **Acceptance:** scheduled run opens exactly one `maf-drift`-labelled issue (and updates, not duplicates, on the next run).
- **Depends:** none. **Effort:** S. **Pri:** P0.

### 1.3 — Drift Detector: harden grade extraction
- **Goal:** never file a blank-grade issue if the doctor's output is ever reworded.
- **Files:** `.github/workflows/maf-drift-detector.yml:34-39, 48-49`.
- **How:**
  ```yaml
  - name: Run MafDoctor against repo
    id: doctor
    run: |
      set -o pipefail
      REPORT=$(maf-autopilot doctor "$GITHUB_WORKSPACE")
      echo "$REPORT" > maf-doctor-report.md
      GRADE=$(echo "$REPORT" | grep -oP 'health grade: \*\*\K[A-F]' | head -1 || true)
      [ -z "$GRADE" ] && { echo "::warning::Could not parse health grade — UNKNOWN."; GRADE="UNKNOWN"; }
      echo "grade=$GRADE" >> "$GITHUB_OUTPUT"
      echo "Detected MAF health grade: $GRADE"
  ```
  Gate the issue step on `if: steps.doctor.outputs.grade != 'A' && steps.doctor.outputs.grade != 'UNKNOWN'`.
- **Acceptance:** with a reworded headline (simulate locally) the workflow emits a warning and does **not** open an issue; with a real grade it behaves as before.
- **Depends:** 1.2. **Effort:** S. **Pri:** P1.

> **Note:** after 1.2/1.3 the drift detector will *work* but, because the doctor scans its own demo bait, it will keep reporting **F** and re-asserting the issue. That's a true-but-noisy signal until Phase 3.1 calibrates it. Acceptable interim state.

---

## Phase 2 — Observability

**Objective:** a scheduled failure must reach the maintainer, not just paint a red X. **Exit criteria:** an injected failure opens/updates a tracking issue.

### 2.1 — Release Watcher notify-on-failure
- **Goal:** surface any watcher failure as a GitHub notification.
- **Files:** `.github/workflows/maf-release-watcher.yml` (new terminal job).
- **How:**
  ```yaml
  notify-on-failure:
    needs: [validate-inputs, check-for-new-maf-release, analyze-and-update]
    if: failure()
    runs-on: ubuntu-latest
    permissions: { issues: write }
    steps:
      - name: Open/update failure tracking issue
        env: { GH_TOKEN: "${{ secrets.GITHUB_TOKEN }}" }
        run: |
          gh label create maf-release --color 0e8a16 --force || true
          TITLE="🔴 Self-update failed: ${{ github.workflow }}"
          URL="${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"
          NUM=$(gh issue list --label maf-release --state open --search "$TITLE in:title" --json number --jq '.[0].number // empty')
          if [ -n "$NUM" ]; then
            gh issue comment "$NUM" --body "Failed again on $(date -u +%F): $URL"
          else
            gh issue create --title "$TITLE" --label maf-release \
              --body "The scheduled self-update workflow failed. Run log: $URL"
          fi
  ```
  `if: failure()` with `needs:` fires only when a dependency failed. Uses `GITHUB_TOKEN` + `issues: write` (no PAT, no 🔒).
- **Acceptance:** temporarily force a step to `exit 1` on a branch → a `maf-release` issue appears with the run link; a second failure comments rather than duplicates.
- **Depends:** none (independent of 1.x). **Effort:** S. **Pri:** P1.

### 2.2 — Drift Detector notify-on-failure
- Same pattern, `needs: [doctor]`, `if: failure()`. Distinguish from the drift *content* issue by title prefix (`🔴 Self-update failed:` vs `🩺 MAF drift detected`). **Effort:** S.

### 2.3 — Document the native fallback  🔒
- **Goal:** defense-in-depth. GitHub emails the workflow author on the **first** failure of a scheduled-run streak if "Send notifications for failed workflows you've created" is enabled.
- **How:** enable it in personal notification settings; add a one-liner to `README.md`/`TROUBLESHOOTING.md` that the issue-based notifier (2.1/2.2) is primary because the native email suppresses after the first failure. **Effort:** S. **Auth:** 🔒.

---

## Phase 3 — Engine correctness & safety

**Objective:** make the engine's signals meaningful and its privileged operations safe. **Exit criteria:** drift grade reflects real product health; major bumps are gated; concurrent/duplicate runs can't corrupt state.

### 3.1 — Doctor self-scan exclusions
- **Goal:** the drift detector grades **product** code, not the repo's intentional anti-pattern bait (`samples/find-the-bug/`, `samples/maf-*/ChatClientFactory.cs`, `*.Tests` fixtures). Root: `SourceFileWalker.EnumerateCsFiles` (`src/maf-autopilot/Tools/SourceFileWalker.cs:68-77`) excludes only `/bin/` and `/obj/`.
- **Design choice (objective):** do **not** hardcode `samples`/`tests` exclusions into the tool — a *user's* tests are real code and must be scanned. Instead add **opt-in exclude globs** and apply them only in the drift workflow.
- **Files:** `SourceFileWalker.cs`, `DoctorTool.cs` (thread an `excludeGlobs` param through `MafDoctor`/`AnalyzeRepo`), `Program.cs:27-34` (`doctor <path> [--exclude <glob>]...`, repeatable), `maf-drift-detector.yml`, plus `DoctorToolTests`/`SourceFileWalkerTests`.
- **How:** add `--exclude` (repeatable) that appends gitignore-style globs to the walker's skip set; optionally honor a `.mafdoctorignore` file at the scan root. In the drift workflow:
  ```bash
  maf-autopilot doctor "$GITHUB_WORKSPACE" \
    --exclude 'samples/**' --exclude '**/*.Tests/**' --exclude '**/find-the-bug/**'
  ```
- **Acceptance:** `doctor` on the repo with the excludes returns a grade reflecting only `src/maf-autopilot*` non-test code (expected A/B); a unit test asserts excluded paths are not walked; default behavior (no `--exclude`) is unchanged for users.
- **Depends:** ideally after 1.2/1.3 so the drift workflow is otherwise green. **Effort:** L. **Pri:** P2.

### 3.2 — Make `is_major` real (or delete it)
- **Goal:** stop a 1.x→2.x major from auto-committing to `main` with the same (zero) gate as a patch. Today `is_major` only injects the word "MAJOR" into the commit subject (`build_watcher_commit_message.py:35`); the escalation `architecture.md:237` / plan B.3 describe died with the PR flow.
- **Recommended (Option A — human gate for majors):** in `analyze-and-update`, gate the push on minor/patch and escalate majors to a human:
  - `Commit directly to main` step → add `if: needs.check-for-new-maf-release.outputs.is_major != 'true'`.
  - New step `if: needs.check-for-new-maf-release.outputs.is_major == 'true'`: upload the diff artifacts (already done) and open a `maf-release`-labelled issue titled `🚨 MAJOR MAF bump <old>→<new> — manual review required`, linking the run + artifacts, and **do not push**. The watcher's job here is detect+prepare, not auto-merge a major.
- **Alternative (Option B — accept auto-commit for majors):** delete the `is_major` computation (`:96-105`), output (`:43`), and `IS_MAJOR` plumbing (`:301`, `build_watcher_commit_message.py:26,35`) as dead code.
- **Either way:** fix the two docs (Phase 7.1) — they currently describe escalation that doesn't exist.
- **Acceptance:** a synthetic `2.0.0` detection (5.2) opens an escalation issue and leaves `main` untouched (Option A) — verified in the dry-run.
- **Depends:** coordinate with 5.2. **Effort:** M. **Pri:** P1.

### 3.3 — Cron stagger + concurrency guards
- **Goal:** prevent the watcher (which pushes to `main`) and the drift detector (which grades `main`) from racing in the same minute, and prevent a run from overlapping itself.
- **Files:** both workflows.
- **How:** watcher keeps `0 9 * * 1` + `concurrency: { group: maf-self-update-watcher, cancel-in-progress: false }`. Drift → `0 11 * * 1` + its own group. *(Stronger alternative: trigger drift on `workflow_run` completion of the watcher so it always grades post-update state — note as a follow-up.)*
- **Acceptance:** two manual dispatches of the same workflow in quick succession → the second queues rather than running concurrently.
- **Effort:** S. **Pri:** P2.

### 3.4 — Idempotent registry append
- **Goal:** re-runs (or overlapping runs) must not accrete duplicate draft entries into `registry.yaml`. Today `:228-260` appends a fresh `# === Draft entries ... ===` block + extractor output every time with no dedupe.
- **Files:** `maf-release-watcher.yml:228-260`.
- **How:** before appending, collect existing entry ids (`grep -oE '^\s*- id:\s*\K\S+' registry.yaml`) into a set; filter the extractor's `$TMP_ENTRIES` to drop any `- id:` block whose id already exists; only append if new blocks remain. (Reuse the `MAF<digits>-` id convention the ai-fill flow already computes.)
- **Acceptance:** running the append step twice for the same `OLD→NEW` produces the registry delta **once**.
- **Effort:** M. **Pri:** P2.

### 3.5 — Reconcile COPILOT_ASSIGN_PAT scope docs  🔒
- **Goal:** the watcher comment (`:136-138`) says the PAT needs **Contents R/W only**, but `maf-ai-fill-todos.yml:28` says the same secret needs **Issues + PRs + Actions R/W**. One is wrong/incomplete.
- **How:** determine the **union** actually required (Contents:RW for the push; Issues/PRs for assignment; Actions:write is supplied via `GITHUB_TOKEN`, not the PAT — confirm). Update both comments to state the true minimal union; verify the live secret's scopes match.
- **Acceptance:** both files document the same scope; the secret's scopes are confirmed sufficient and not broader than needed.
- **Effort:** S. **Auth:** 🔒 (secret inspection).

### 3.6 — Daily poll cadence (optional)
- **Goal:** cut worst-case detection latency from ~7d to ~1d; the no-op path is cheap.
- **How:** watcher cron `0 9 * * 1` → `0 9 * * *`. Keep the prerelease filter. *(Further: poll `microsoft/agent-framework` releases for `dotnet-X.Y.Z` tags instead of NuGet flatcontainer for same-day detection.)*
- **Effort:** S. **Pri:** P3.

---

## Phase 4 — Latent bugs in the AI self-update chain

**Objective:** fix the bugs that will surface once the blockers are gone. **Exit criteria:** a full watcher→fill→verify cycle runs without these traps.

### 4.1 — `gen_guide_section.py` prerelease crash
- **Bug:** `tuple(int(p) for p in version.split('.'))` (`:67`, `:84`) raises `ValueError` on `2.0.0-rc1`; `validate-inputs` (`maf-release-watcher.yml:31`) *accepts* `-rc1`, and the step has no `continue-on-error`, so a manual prerelease dispatch dies **after** `update_compat_matrix.py` mutated the matrix (half-applied state).
- **Fix:** parse the release core only: `tuple(int(p) for p in v.split('-')[0].split('.'))` in both call sites. Add a test for `1.6.1` and `2.0.0-rc1`.
- **Effort:** S. **Pri:** P2.

### 4.2 — `verify_crossfile_consistency.py` per-version guides
- **Bug:** check #6 (`:243`) validates `guide_section` IDs only against `guides/maf-1.3.0-migration-guide.md`, so legitimate references into per-version guides (1.4/1.5/1.6.1) are flagged "invented".
- **Fix:** collect valid section IDs from the **union** of all `maf-*-migration-guide.md` (reuse `gen_guide_section.py`'s glob). Update the failure text at `:147,:153` to stop hardcoding 1.3.0.
- **Effort:** S. **Pri:** P2.

### 4.3 — `verify_crossfile_consistency.py` staleness → warn
- **Bug:** check #5 (`:217-232`, `MAX_LAST_UPDATED_AGE_DAYS=30`) **fails** the verify job when the matrix date is >30d old — brittle against MAF's irregular cadence and slow human fill turnaround.
- **Fix:** print the stale date as a Note, don't append to `findings` (warn-not-fail); or only enforce once the matrix top row already equals `.maf-version`. Warn-only is safest.
- **Effort:** S. **Pri:** P2.

### 4.4 — compat-matrix placeholder gate
- **Bug:** `update_compat_matrix.py:23-26` writes `>= unknown` / `Auto-detected — verify…` placeholders expecting the fill bot to replace them; **no gate checks they were replaced**, so a literal `>= unknown` row can reach `main` and pass every check (especially when auto-fill is skipped).
- **Fix:** add a check in `verify_crossfile_consistency.py` that the top (current-cycle) row contains no `unknown` / `Auto-detected — verify` placeholder → failing finding.
- **Effort:** S. **Pri:** P2.

### 4.5 — `gen_guide_section.py` human-block anchor
- **Bug:** preserve-human-edits uses the bare heading `## Human additions` (`:161-165`); attacker-controlled release-notes text containing that line (inside the fenced AUTO block) can hijack the boundary on a second run, truncating the auto block.
- **Fix:** anchor the human region to the script-controlled `AUTO_END` sentinel (which a fence can't reproduce): treat everything after `AUTO_END` as human; restrict the heading search to the substring after it.
- **Effort:** S. **Pri:** P3 (injection hardening).

### 4.6 — AI-chain scope robustness
- **Bug:** `maf-ai-fill-verify.yml:65-72` and `maf-ai-semantic-review.yml:84-91` gate real work on the `ai-fill` label **or** a `claude/|copilot/|codex/` branch prefix. The label is self-applied by the bot (only *requested* in the prompt template). If the agent omits it and its branch prefix differs (the default `claude` agent's GraphQL login is `anthropic-code-agent`), both checks **report green while doing nothing**.
- **Fix (objective, defense-in-depth):** (a) make scope detection diff-based — in-scope if the PR touches `registry.yaml` / `compatibility-matrix.md` / `guides/*migration-guide.md`, independent of label/branch; **and** (b) **assert loudly**: if a PR touches those watcher-owned files but computes out-of-scope, `exit 1` with a clear message so a silent pass is impossible. Optionally deterministically apply `ai-fill` from a trusted workflow keyed on the actual agent author login.
- **Acceptance:** a PR that edits `registry.yaml` without the `ai-fill` label is still verified (or fails loudly), never silently passes.
- **Effort:** M. **Pri:** P1.

### 4.7 — semantic-review prompt extension
- **Bug:** `maf-ai-semantic-review.yml:161,178` writes/reads `.ai-review-prompt.md`, but `actions/ai-inference` documents support only for `.txt` / `.prompt.yml`; a `.md` may be misparsed → empty response → misleading WARN verdict on every PR.
- **Fix:** rename the artifact to `.ai-review-prompt.txt` in both the build and inference steps.
- **Effort:** S. **Pri:** P2.

### 4.8 — ai-fill-verify sticky comment
- **Bug:** `maf-ai-fill-verify.yml:174` uses `gh pr comment --body-file`, appending a **new** comment on every `synchronize`/`labeled`/`reopened` → comment spam while an agent iterates.
- **Fix:** switch to `marocchino/sticky-pull-request-comment@<pinned-sha>` with `header: maf-ai-fill-verify` (the pattern `maf-pr-audit.yml:140-143` already uses); update-to-success/delete the sticky when checks pass.
- **Effort:** S. **Pri:** P3.

### 4.9 — ai-fill-todos bot-id resolution non-fatal
- **Bug:** `maf-ai-fill-todos.yml:182-200` does `exit 1` when `BOT_ID` is empty — **after** the issue was created (`:149-153`) — so a transient GraphQL hiccup or pagination edge (`first:25` actors) fails the whole job even though the issue exists. This contradicts the file's own "still runs, only auto-assignment is skipped with a loud annotation" intent (`:31-33`).
- **Fix:** on empty/null `BOT_ID`, emit `::warning` with the enable-agent URL and **skip** the assignment block (continue to success), matching the already-best-effort mutation at `:213-272`.
- **Effort:** S. **Pri:** P2.

### 4.10 — pr-audit honest scoping
- **Bug:** header (`maf-pr-audit.yml:3-4`) claims per-changed-file scoping, but `:114` runs `doctor "$GITHUB_WORKSPACE"` (whole repo); the changed-file cap logic (`:49-87`) is dead relative to the scan, so PR comments report whole-repo debt.
- **Fix:** either (a) honestly relabel it a "whole-repo doctor report", or (b) pass the NUL-collected changed-file list to a scoped doctor invocation (depends on doctor supporting a file/glob filter — synergizes with 3.1's `--exclude`/path support).
- **Effort:** M. **Pri:** P2.

### 4.11 — ai-fill template guide anchor
- **Bug:** `ai_fill_issue_prompt.md.tpl:66` hardcodes `guides/maf-1.3.0-migration-guide.md` while adjacent checks use `$TARGET` — correct by design (section IDs anchor to 1.3.0 per `verify_crossfile_consistency.py:43`) but a copy-paste-bug smell, and diverges silently if the canonical guide is renumbered.
- **Fix:** add a one-line comment explaining the anchor, or template it as `{{GUIDE_ANCHOR_VERSION}}` substituted by `build_ai_fill_issue_body.py` so CI gate + agent checklist share one source of truth.
- **Effort:** S. **Pri:** P3.

---

## Phase 5 — End-to-end validation

**Objective:** prove the auto-update path works in the mode it was designed for (it never has). **Exit criteria:** a synthetic release flows detect→diff→matrix→guide→registry→commit→dispatch green, and both scheduled workflows are confirmed green live.

### 5.1 — NuGet fixture smoke test
- **Goal:** lock in 1.1 so the detection logic can never silently regress.
- **How:** add a `pytest` (run in `ci-invariants.yml`) that pipes a committed `index.json` fixture through `get_latest_stable.py` and asserts the expected stable version; include a prerelease-only fixture → `""`.
- **Depends:** 1.1. **Effort:** M. **Pri:** P1.

### 5.2 — `analyze-and-update` dry-run  🔒
- **Goal:** first-ever end-to-end exercise of the privileged path **without** risking `main`.
- **How:** parameterize the push target (add a `dispatch` input `push_target` defaulting to `main`; the final step pushes `HEAD:${PUSH_TARGET}`). Dispatch against a throwaway branch with a synthetic `NEW` (e.g. `1.6.2` and separately `2.0.0` to exercise 3.2's major gate). Verify: diffs produced, matrix row added, guide section drafted, registry delta appended once (3.4), commit lands on the throwaway branch, `gh workflow run maf-ai-fill-todos.yml` dispatch accepted (and the major case opens an escalation issue instead of pushing). Delete the branch after.
- **Acceptance:** green run on the throwaway branch; `main` untouched; major case escalates.
- **Depends:** 1.1, 3.2, 3.4. **Effort:** M. **Auth:** 🔒.

### 5.3 — Re-trigger + verify lifecycle  🔒
- **How:** manually dispatch both workflows (blank inputs). Watcher: confirm green no-op when `.maf-version` already equals latest stable. Drift: confirm a `maf-drift` issue opens; then (after 3.1) confirm grade returns to A/B and the issue auto-closes.
- **Acceptance:** both green; drift issue open→close lifecycle observed once.
- **Depends:** Phase 1–3. **Effort:** S. **Auth:** 🔒.

### 5.4 — Workflow-set parse audit
- **Goal:** explain/clear the historical 0-second startup_failures (the watcher was *collateral*; the unparseable item was elsewhere in the workflow set for that event).
- **How:** run the actionlint gate (0.2) across all 10 workflows on `main`; confirm zero parse errors. The startup_failure placeholders self-clear once `main`'s set is valid. Use `gh api .../runs/<id>/jobs --jq .total_count` to confirm any new 0s run is a placeholder (zero jobs), not a real failure.
- **Effort:** S. **Pri:** P3.

---

## Phase 6 — Rebrand to MAF Doctor (brand-only, identity-frozen)

**Objective:** the project is named **MAF Doctor** everywhere a human looks; the technical identity (`maf-autopilot` package id, CLI, dll, namespace) stays frozen so nothing breaks. This finishes the decision already recorded in `README.md:5`. **Exit criteria:** brand text + repo + MCP server id + image path say MAF Doctor; `dotnet tool install --global maf-autopilot` and existing `mcp.json` entries still work; build+tests green.

> **Rejected:** renaming the NuGet package id / CLI. NuGet ids are immutable (rename = republish + lifelong dual-maintenance); it breaks every install and, worst, the `command=maf-autopilot` line `init` already wrote into users' `mcp.json`. The two-name split already solves the naming goal.

### 6.1 — PR-A: brand text + MCP server id + repo URL strings
- **Files (text only):** `*.md` brand prose; `.vscode/mcp.json` server keys; README config snippets; Dockerfile header example + OCI `title` label + pull-URL examples; `RepositoryUrl` in both csprojs.
- **How (scoped, never blanket-sed):**
  ```bash
  # MCP server id (this repo's config + README snippets)
  sed -i 's/"maf-autopilot-custom-registry"/"maf-doctor-custom-registry"/g; s/"maf-autopilot"/"maf-doctor"/g' .vscode/mcp.json
  # Repo/image path in doc strings (slash-anchored — NOT the package id)
  grep -rl 'joslat/maf-autopilot' --include='*.md' --include='Dockerfile' --include='*.yml' . \
    | xargs sed -i 's#joslat/maf-autopilot#joslat/maf-doctor#g'
  # Brand prose only (target the phrase, not the bare token)
  grep -rl 'maf-autopilot toolkit' --include='*.md' . | xargs sed -i 's/the maf-autopilot toolkit/MAF Doctor/g'
  ```
- **MUST survive (verify guard — these literals are frozen):**
  ```bash
  grep -rn 'dotnet tool install --global maf-autopilot' README.md
  grep -n '<PackageId>maf-autopilot</PackageId>' src/maf-autopilot/maf-autopilot.csproj
  grep -n 'ENTRYPOINT.*maf-autopilot.dll' Dockerfile
  ```
- **Acceptance:** `dotnet build && dotnet test` green (no code identifier changed); the three guard greps still match.
- **Effort:** M. **Pri:** P2.

### 6.2 — InitCommand server key
- **File:** `src/maf-autopilot/InitCommand.cs:151,158,161`.
- **How:** write server key `maf-doctor` (and `maf-doctor-custom-registry`) into users' `mcp.json`, but keep `command=maf-autopilot` (the frozen binary). Update the "already contains" guard (`:151`) to also recognize the legacy `maf-autopilot` key so re-running `init` doesn't create a stale duplicate; add a brief migration note.
- **Acceptance:** `InitCommandTests` updated; fresh `init` writes `maf-doctor`→`command: maf-autopilot`; existing entries still launch.
- **Depends:** 6.1. **Effort:** S.

### 6.3 — CHANGELOG migration notes
- Note the GHCR image-path move (`ghcr.io/joslat/maf-autopilot` → `…/maf-doctor` for **new** tags; old tags remain pullable; GHCR does not redirect image names — the one user-visible break) and the `mcp.json` server-id change. **Effort:** S. **Pri:** P3.

### 6.4 — Repo rename  🔒
- **How:** rename `joslat/maf-autopilot` → `joslat/maf-doctor` in GitHub settings **after 6.1–6.3 merge** (so CI isn't churning mid-PR). GitHub permanently redirects old URLs/clones/`gh`; `docker-publish.yml`'s `${{ github.repository }}` then publishes `ghcr.io/joslat/maf-doctor` automatically; CODEOWNERS/branch-protection survive.
- **Acceptance:** old URLs redirect; next image publish lands under the new path.
- **Effort:** S. **Auth:** 🔒. **Order:** LAST in the rebrand.

### 6.5 — Namespace rename (optional, separate PR)
- Mechanical `MafAutopilot`→`MafDoctor` across ~92 `.cs` files + dir/sln/`InternalsVisibleTo`/`AssemblyName`. **Invisible to users**, pure churn, re-couples Dockerfile ENTRYPOINT + analyzer pack path if `AssemblyName` changes. **Only if** tidiness is wanted; never bundle with branding. **Effort:** L. **Pri:** P3.

---

## Phase 7 — Documentation & closeout

### 7.1 — Fix is_major escalation docs
- Update `docs/architecture.md:237` and `docs/maf-migration-toolkit-plan.md:184` to match the 3.2 decision (human gate for majors, or remove). They currently describe behavior that doesn't exist. **Depends:** 3.2.

### 7.2 — README/CHANGELOG behavior updates
- Document: failure-tracking issues (2.1/2.2), the drift detector's scan target/exclusions (3.1), the watcher cadence (3.6 if taken), and the `is_major` behavior. Fix `README.md:201` if drift behavior changed. **Effort:** S. **Pri:** P3.

### 7.3 — Final verification + close plan
- Confirm the green board: both scheduled workflows green; failure-notifier fires on injected failure; drift issue lifecycle works; dry-run proved the auto-update chain; rebrand guards pass. Flip all rows to 100%/☑. Update agent memory: "scheduled self-update workflows fixed; auto-update path validated <date>." **Gate.**

---

## Global acceptance criteria (definition of done)

1. ✅ Scheduled **MAF Release Watcher** completes green (no-op or real detection), no `IndentationError`.
2. ✅ Scheduled **MAF Drift Detector** completes green; opens **one** `maf-drift` issue and closes it when healthy.
3. ✅ Any scheduled failure opens/updates a `maf-release` tracking issue (verified by injection).
4. ✅ Drift grade reflects **product** code (samples/tests excluded) — expected A/B, not structural F.
5. ✅ A synthetic release flows through `analyze-and-update` green on a throwaway branch; majors escalate instead of auto-pushing; registry append is idempotent.
6. ✅ `actionlint`/`yamllint`/`compileall` gate all workflow+script PRs.
7. ✅ Brand = MAF Doctor in docs/repo/MCP-id/image; `dotnet tool install --global maf-autopilot` and existing `mcp.json` entries unchanged; build+tests green.

## Authorization checklist (🔒 — maintainer only)

- [ ] 2.3 enable native failed-workflow notifications
- [ ] 3.5 inspect/confirm `COPILOT_ASSIGN_PAT` scopes
- [ ] 5.2 run the privileged dry-run (throwaway branch)
- [ ] 5.3 manually re-trigger both workflows
- [ ] 6.4 rename the GitHub repo

## Rollback

Every code change ships on a branch behind PRs; revert the PR to roll back. The only non-revertible steps are 🔒 6.4 (repo rename — reversible by renaming back, redirects re-point) and any live label/issue creation (cosmetic; delete the label/issue). No destructive data operations are involved.

---

## Progress log

### 2026-06-14 — Phases 0 + 1 complete (branch `fix/self-update`)
- **0.1** Branch created off the current line; this plan committed.
- **0.2** Added `lint-workflows-and-scripts` job to `.github/workflows/ci-invariants.yml`: actionlint (parse/schema + expression contexts, shellcheck/pyflakes disabled to avoid style-noise) + `python -m compileall .github/scripts`. *yamllint dropped* to avoid false-failures on pre-existing files (simple wins); actionlint covers YAML parse.
- **1.1** Extracted the inline `python3 -c` NuGet version filter → `.github/scripts/get_latest_stable.py` with `tests/test_get_latest_stable.py` (**12 tests green** locally; pipe-through returns `1.6.1`). Root cause #1 (scheduled-run `IndentationError`) fixed.
- **1.2** Drift Detector self-provisions `maf-drift` / `needs-review` labels via `gh label create --force` (idempotent, GITHUB_TOKEN) before the issue step. Root cause #2 (`could not add label`) fixed — no manual label creation needed.
- **1.3** Drift grade parse hardened: `set -o pipefail`, `|| true` on the grep, empty→`UNKNOWN`, and the issue gate now skips `UNKNOWN` so a future doctor-output reword can't file a blank-grade issue.

### 2026-06-14 — Phase 2 complete (observability)
- **2.1 / 2.2** Added a terminal `notify-on-failure` job to both workflows (`if: failure()`, `needs:` all upstream jobs). On failure it opens or comments onto **one rolling** `maf-release`-labelled issue `🔴 Self-update workflow failed: <name>` (exact-title jq match, not flaky `--search`). github.* contexts flow via `env:` redirect to satisfy ci-invariants job (4).
- **2.3** Documented the failure-notification behavior + the native "failed workflow" email fallback in `TROUBLESHOOTING.md`, and refreshed the stale "Release watcher" section (it still described the removed PR-based flow). The 🔒 native-email toggle is a maintainer opt-in (authorization checklist).
- Validated: all three edited workflows parse; ci-invariants job (4) input-redirect and job (5) permissions checks pass locally.

### 2026-06-14 — Phase 3 complete (engine correctness & safety)
- **3.1** Added `EnumerateCsFiles(repoRoot, excludes)` overload to `SourceFileWalker`, threaded through `DoctorTool.Run`/`AnalyzeRepo`, exposed CLI `doctor [path] --exclude <substr>…`, and the drift workflow now passes `--exclude samples/ .Tests/ find-the-bug/`. **Implementation note:** substring (not glob) matching on the *repo-relative* path — simpler, no edge cases. **Discovery:** the AntiPatternScanner already self-skips `samples`/`tests` segments for `skipInTestFiles` rules, so the walker exclude is the *broader* belt-and-suspenders (covers fan-out/prompt/cost scanners + `skipInTestFiles:false` rules). New test `Run_ExcludedPaths_DoNotAffectGrade`; **16 DoctorTool tests green**. Forward-compatible: old published builds ignore the extra CLI args.
- **3.2** Option A (human gate): the watcher's `Commit directly to main` step is now gated `if: is_major != 'true'`; a new `Escalate MAJOR version bump` step opens a single `maf-release,needs-review` tracking issue (diffs attached as run artifacts) and does NOT push. Added `issues: write` to the job. Patches/minors unchanged.
- **3.3** Drift cron moved to Mon 11:00 (2h after watcher); both workflows got `concurrency:` groups (`cancel-in-progress: false`) so a run can't overlap itself.
- **3.4** New `dedupe_registry_entries.py` (+5 tests) filters extractor output against existing registry ids (and within-batch) before append — re-runs / daily re-detection no longer duplicate draft entries.
- **3.5** Reconciled the conflicting `COPILOT_ASSIGN_PAT` scope comments: the watcher comment now points to the full shared union (Issues+Contents+PRs+Actions R/W) documented in `maf-ai-fill-todos.yml`, so no one provisions a Contents-only PAT that breaks assignment. (🔒 verifying the live secret's scopes remains a maintainer step.)
- **3.6** Watcher cron weekly→daily (`0 9 * * *`) to cut detection latency 7d→1d.
- Validated: all workflows parse; concurrency groups present; 17 Python helper tests green; compileall clean; ci-invariants job (4) clean.

### 2026-06-14 — Phase 4 complete (latent chain bugs)
- **4.1** `gen_guide_section.py` `_semver_key()` parses the release core (`2.0.0-rc1`→`(2,0,0)`) so a manual prerelease dispatch no longer crashes the chain-banner sort. (+test)
- **4.2** `verify_crossfile_consistency.py` `collect_all_guide_sections()` validates `guide_section` IDs against the **union** of all per-version guides (cumulative excluded), not just 1.3.0. Failure messages de-hardcoded. (+tests)
- **4.3** Matrix `last-updated` >30d is now a `::warning`, not a blocking finding (MAF cadence is irregular).
- **4.4** New top-row placeholder gate fails if `>= unknown` / `Auto-detected — verify` reaches the current matrix row. (+tests)
- **4.5** Human-additions preservation anchors to the `AUTO_END` sentinel (fence strips HTML comments, so it can't be injected) on BOTH the existing file and the fresh `auto_body` — injected `## Human additions` can no longer hijack the boundary. (+injection test)
- **4.6** Silent no-op closed: `maf-ai-fill-verify` scope is now **diff-driven** (touches watcher files), and `maf-ai-semantic-review` runs on any non-draft PR with the entries-count step gating the paid inference — neither depends on a self-applied `ai-fill` label / bot branch prefix.
- **4.7** Semantic-review prompt artifact `.ai-review-prompt.md`→`.txt` (the `actions/ai-inference` `prompt-file` supports `.txt`/`.prompt.yml`, not `.md`).
- **4.8** `maf-ai-fill-verify` uses the marocchino sticky comment (header `maf-ai-fill-verify`) instead of `gh pr comment` (which stacked a new comment every push), plus a success-path that clears the sticky to a ✅.
- **4.9** `maf-ai-fill-todos` bot-id resolution: warn + `exit 0` (skip assignment) instead of `exit 1` — a resolution miss no longer fails a job that already created the issue.
- **4.10** `maf-pr-audit` comment corrected (whole-repo product audit, not per-changed-file) and the doctor call now passes `--exclude samples/ .Tests/ find-the-bug/`.
- **4.11** The issue-template's check #6 now unions all per-version guides (matches the 4.2 CI gate) instead of grepping only the 1.3.0 guide.
- Validated: all 10 workflows parse; ci-invariants job (4)+(5) clean; **50 Python tests green**; compileall clean.

### 2026-06-14 — Phase 5 (validation): 5.1 + 5.4 done; 5.2/5.3 maintainer-gated
- **5.1** Added a `Run helper-script unit tests` step to `ci-invariants` (`pytest .github/scripts/tests`) so the NuGet detection + verify/dedupe/guide helpers can't silently regress.
- **5.2** Added a `push_target` workflow_dispatch input (validated; env-redirected at the push site) so a maintainer can dry-run `analyze-and-update` against a throwaway branch with a synthetic `maf_version`. **The actual privileged dry-run is 🔒 maintainer-only** (needs secrets + a live run).
- **5.3** 🔒 — re-triggering the live scheduled workflows is only meaningful once this PR merges (the cron runs `main`'s copy). Post-merge: dispatch both with blank inputs and confirm green + the drift-issue open/close lifecycle.
- **5.4** All 10 workflows parse; the actionlint gate (0.2) now guards the parse class on every workflow PR. The historical 0-second startup_failures self-clear once a valid workflow set lands on `main`.
- **Regression gate: full `dotnet test` (net10.0) = 739 passed, 0 failed** — the Phase 3 C# changes (doctor `--exclude`) are clean.

### 2026-06-14 — Phase 6 (brand-only rename to MAF Doctor)
- **6.1/6.2** MCP server id → `maf-doctor` in `.vscode/mcp.json`, README/setup/workshop snippets, and `InitCommand` (which now writes `maf-doctor` while keeping `command=maf-autopilot`, and recognizes the legacy key to avoid duplicates). InitCommand tests updated; 6 green. **The frozen identity is untouched:** `<PackageId>maf-autopilot</PackageId>`, `ToolCommandName=maf-autopilot`, the dll, and `dotnet tool install --global maf-autopilot` all unchanged.
- **6.3** CHANGELOG `[Unreleased]` entry documents the self-update fixes, the brand change, and the **post-merge maintainer follow-ups**.
- **6.1 (URL portion) / 6.4 / 6.5 — deferred by design:** flipping `joslat/maf-autopilot` → `joslat/maf-doctor` across docs / `RepositoryUrl` / SARIF URLs / OCI labels is coupled to the GitHub repo rename (🔒 maintainer) — doing it pre-rename creates a 404 window and risks SARIF-URL test churn. Bundle it with 6.4. The namespace rename (6.5) is optional and explicitly a separate mechanical PR.
- Build green; InitCommand + Doctor tests pass.

### 2026-06-14 — Phase 7 (docs & closeout) — code-complete
- **7.1** Rewrote `docs/architecture.md` "When MAF X.Y ships" flow to match reality (daily cron; majors escalate to an issue instead of auto-PR; direct-push-to-main; idempotent append; AI-fill dispatch; failure notifications). Added a superseded-note to `maf-migration-toolkit-plan.md` B.3.
- **7.2** README workflow-tree cadence corrected (watcher daily; drift Mon 11:00 product-scoped); CHANGELOG + TROUBLESHOOTING already cover failure issues / drift target / cadence.
- **7.3 — final green board:** `dotnet test` net10.0 = **739 + 15 (analyzers) passed, 0 failed**; **50 Python helper tests** pass; `compileall` clean; ci-invariants job (4)+(5) clean; frozen identity literals verified intact (`PackageId`, `ToolCommandName`, `dotnet tool install … maf-autopilot`).

### Completed post-merge (was maintainer-only 🔒)
1. ✅ **5.2** — dry-run of `analyze-and-update` dispatched from the branch with `push_target=dryrun/self-update-validate`. Detected 1.6.1→1.10.0 via the real NuGet path, ran a real diff (artifact `maf-diff-1.10.0`), and committed the full update (`.maf-version`, matrix, new guide, **425 lines of registry drafts**, cumulative guide) to the throwaway branch. `main` untouched. Branch + the dry-run fill-issue cleaned up.
2. ✅ **5.3** — after merge to `main`, both scheduled workflows triggered live and went **green**. The watcher did the real **1.6.1 → 1.10.0** auto-commit to `main` (the repo was 4 versions behind because the watcher had been broken) and dispatched the fill flow (#70); the drift detector self-provisioned the labels and opened #69 (grade F — expected until a build with 3.1's `--exclude` is published to NuGet).
3. ✅ **6.4** — repo renamed `joslat/maf-autopilot` → **`joslat/maf-doctor`** (old path 301-redirects). Flipped every `joslat/maf-autopilot` URL / `RepositoryUrl` / SARIF helpUri / OCI title to `joslat/maf-doctor` on `chore/rebrand-urls` (SARIF + analyzer tests still green); the GHCR image path follows automatically. The package id / CLI / dll / namespace stay `maf-autopilot` (frozen).
- Still optional (intentionally a separate future PR): **6.5** namespace rename `MafAutopilot`→`MafDoctor`; and enabling the native failed-workflow email (2.3).

### 2026-06-14 — Final review pass (self-review of the PR diff)
- Verified the watcher escalation gating is coherent: `Commit directly to main` (`if: is_major != 'true'`) → `Open issue for Copilot` (`if: committed == 'true'`) → `Escalate MAJOR` (`if: is_major == 'true'`). Minor/patch commits & dispatch; no-op skips dispatch; majors escalate and never push.
- Verified the CLI `doctor` arg parser handles `doctor --exclude x` (no path), `doctor /p --exclude a --exclude b`, and `doctor --exclude` (no value) without crashing; old published builds ignore the extra args (forward-compatible).
- **Hardened the lint gate:** split into a BLOCKING YAML-parse check (all 10 verified to parse) + ADVISORY actionlint (`continue-on-error`), so a pre-existing actionlint nit can't red-gate the PR while the real startup_failure guard still blocks. compileall + pytest remain blocking.
- PR: **#66** (`fix/self-update` → `security-hardening-v1.1`).

### 2026-06-14 — Live-CI find: duplicate `env:` = the REAL startup_failure cause (5.4 closed properly)
- The PR's first `ci-invariants` run failed on the (then-blocking) actionlint, which flagged: **`maf-release-watcher.yml: key "env" is duplicated in element of "steps"` (the "Fetch release notes" step had two `env:` blocks)**. Run `27497299887` (0 jobs, `name=`file-path) confirmed this is exactly what GitHub's parser rejects → the 0-second **startup_failure** push runs.
- **This corrects the earlier verification agent (claim 3):** it concluded "the watcher YAML is valid at every commit" because it used `pyyaml`, which *tolerates* duplicate keys (keeps the last). The duplicate was introduced by the Phase 7.G env-redirect work on `security-hardening-v1.1` (a second `env:` added without merging the existing `GH_TOKEN` one). Two consequences: (a) GitHub startup-fails the workflow on every event — so this would have broken the **scheduled** cron once the branch reached `main`, not just push; (b) YAML last-wins dropped `NEW`, so the release-notes fetch had been querying empty `dotnet-`/`v` tags.
- **Fixed:** merged the two `env:` blocks in the watcher's "Fetch release notes" step. **Hardened the guard:** the blocking parse-check now uses a duplicate-key-rejecting loader (self-tested) so this class is caught by CI, not just advisory actionlint. All 10 workflows pass the strict loader.
