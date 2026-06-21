# Find the bug in 30 seconds

A pre-workshop teaser. Three ~20-line C# snippets. Each contains ONE subtle
MAF anti-pattern. You have **30 seconds total** to spot the bugs in all
three — no scrolling, no compiling, no Googling.

Then run the toolkit. It finds all 3 in under a second.

## How to play (workshop facilitator script)

1. **Set the stage (30 s).** "I'm about to show you three snippets of MAF
   code. Each has ONE bug. You have 30 seconds to spot all three. No
   compiling, no looking things up. Ready?"

2. **Show the snippets (30 s).** Open all three side-by-side. Either as
   separate VS Code tabs, or three slide columns, or a printed handout.
   Time it.

3. **Take a poll (30 s).** Show of hands: who spotted ALL THREE? TWO? ONE?
   ZERO?

4. **Run the toolkit (10 s).**

   ```bash
   maf-doctor doctor samples/find-the-bug/
   ```

   Result: 3 errors + 2 silent-starvation risks, found in ~850 ms. The
   answer key (below) shows what each one is.

5. **Land the punchline (30 s).** "Even senior MAF developers need
   roughly 5 minutes per file to spot these patterns visually. The
   toolkit found all 3 in 850 milliseconds — and it doesn't get tired,
   doesn't have a bad day, doesn't forget to check the patterns added
   last week. That's the value: predictability + scale."

## Snippets

- [`snippet-1.cs`](./snippet-1.cs) — agent setup. ~20 lines.
- [`snippet-2.cs`](./snippet-2.cs) — fan-out investigator executor.
  ~20 lines.
- [`snippet-3.cs`](./snippet-3.cs) — workflow wiring. ~20 lines.

The bugs are real anti-patterns from `.github/skills/maf-obsolete-api-registry/registry.yaml`
and `src/maf-autopilot/Tools/AntiPatternScannerTool.cs`. Don't read the
[answer key](./answer-key.md) before the audience does the exercise.

## Why these specific bugs

- **Snippet 1** — top-level `Instructions` is famously sneaky: the property
  has been REMOVED from `ChatClientAgentOptions` since MAF 1.0.0 GA, but
  the pre-1.0 docs and several blog posts still show it. Customer code
  copy-pasted from older docs won't compile on any 1.0+ MAF but the
  developer expects it to work.

- **Snippet 2** — non-generic `ValueTask` return on a fan-out
  `[MessageHandler]`. Looks like a perfectly normal async method.
  Silently breaks the fan-in barrier — the workflow exits cleanly with
  missing data. No CS warning, no runtime exception. Pure value loss.

- **Snippet 3** — `AddFanInBarrierEdge(target, sources)` is the OBSOLETE
  overload. Still in the assembly, marked `[Obsolete]` — but the compiler
  warning is easy to dismiss as "just an Obsolete". The arg order looks
  symmetric to a casual reader. The new overload swaps the order, which
  means the WRONG topology is silently wired if you use the obsolete one.

All three are real patterns the toolkit catches today. None are contrived.
