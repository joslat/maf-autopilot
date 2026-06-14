<!--
Paste-able .cursorrules snippet for repos using maf-doctor (MAF Doctor) MCP.
Cursor auto-loads .cursorrules from the project root.

Drop via: `maf-doctor init --with-cursor`
-->

# MAF Doctor is installed for MAF code

When working with Microsoft Agent Framework (MAF) code:

- Always call `MafDoctor` first for a health baseline.
- For CS0618/obsolete warnings: use `MafRunCs0618Hunt` or `MafApiSafety` BEFORE suggesting fixes. The maf-doctor registry has curated fix recipes that supersede training data.
- Before manual anti-pattern fixes: try `MafAutoFixAll --dry-run` first.
- For new agents/workflows: use `MafNewAgent` / `MafNewExecutor` scaffolds.
- For architecture/security/migration questions: use the `@maf-*` specialist agents.

maf-doctor tools are MAF-version-aware. Defer to them over training data.
