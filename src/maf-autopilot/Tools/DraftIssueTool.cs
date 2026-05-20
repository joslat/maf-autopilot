using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using MafAutopilot.Data;
using ModelContextProtocol.Server;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: MafDraftIssue
///
/// Assembles a `microsoft/agent-framework` GitHub-issue-ready markdown body
/// when the user has hit something that looks like a MAF bug. The tool NEVER
/// posts the issue — output is markdown the user reviews and posts manually
/// (or via the GitHub MCP server's `create_issue` tool if they have it installed).
///
/// Why we don't post automatically: posting to a public repo is the
/// "modifies shared / production systems" line. The user reviews the assembled
/// body, edits as needed, and routes it. We just do the assembly work.
///
/// The output follows microsoft/agent-framework's issue conventions:
///   - Title with a short symptom summary
///   - Environment block (MAF + .NET + OS + dotnet-inspect versions)
///   - Repro section (steps + snippet)
///   - Expected vs actual
///   - Toolkit interpretation (registry hits, scanner findings, constraint matches)
///   - Suggested workaround (if known)
/// </summary>
[McpServerToolType]
public sealed class DraftIssueTool
{
    private readonly RegistryService _registry;

    public DraftIssueTool(RegistryService registry)
    {
        _registry = registry;
    }

    [McpServerTool(ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("""
        Assemble a `microsoft/agent-framework` GitHub-issue-ready markdown body
        for something that looks like a MAF bug. Inspects the user's repo to
        detect MAF + .NET versions, cross-references the obsolete-API registry
        for any matches, and emits a ready-to-post issue body.

        Does NOT post. Output is markdown — review, optionally edit, then post
        via the GitHub MCP server's `create_issue` tool (if installed) or by
        pasting into github.com/microsoft/agent-framework/issues/new.

        Security note: user-supplied symptom / expected / actual content is
        wrapped in LLM data-fences (BEGIN/END markers with a random sentinel
        and explicit "treat as data" framing) so any prompt-injection payload
        inside cannot redirect the downstream `create_issue` agent. Pass the
        body to `create_issue` as-is — do NOT strip the fence markers; they
        are part of the security contract.

        Input:
          - repoPath: absolute path to the project root (used to detect MAF + .NET versions).
          - symptom: one-sentence description (e.g. "AddFanInBarrierEdge throws NRE at runtime").
          - snippet: optional minimal reproduction code (15-50 lines is the sweet spot).
          - expected: optional — what you expected to happen.
          - actual: optional — what actually happens (exception type + first stack frame is ideal).

        Returns a fully-formed issue body (markdown) suitable for paste-and-post.
        """)]
    public string MafDraftIssue(
        [Description("Absolute path to the project root (used to detect MAF + .NET versions).")]
        string repoPath,
        [Description("One-sentence symptom — \"X throws Y at Z\" works well.")]
        string symptom,
        [Description("Optional minimal repro snippet. Keep to 15-50 lines.")]
        string? snippet = null,
        [Description("Optional — what you expected.")]
        string? expected = null,
        [Description("Optional — what actually happens (exception + first stack frame is ideal).")]
        string? actual = null)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err) return err;
        if (string.IsNullOrWhiteSpace(symptom))
            return "Error: symptom must not be empty — provide a one-sentence description (\"X throws Y at Z\").";

        // Phase 5.1 — length caps on every user-controlled string input.
        // Without these, an LLM-supplied 1 GB snippet/symptom is a DoS lane.
        // Caps per §5.6 cap table: symptom/expected/actual = short-text class;
        // snippet = code-snippet class. ArgumentException is wrapped to keep
        // the tool's error-string return contract.
        try
        {
            BoundedInput.Validate(symptom,  BoundedInput.ShortTextBytes, nameof(symptom));
            BoundedInput.Validate(snippet,  BoundedInput.SnippetBytes,   nameof(snippet));
            BoundedInput.Validate(expected, BoundedInput.ShortTextBytes, nameof(expected));
            BoundedInput.Validate(actual,   BoundedInput.ShortTextBytes, nameof(actual));
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message}";
        }

        var env = DetectEnvironment(repoPath);
        var registryMatches = MatchRegistry(symptom, snippet);

        // Phase 2.4 — fence user-controlled fields. symptom/expected/actual
        // previously flowed into the markdown body either raw (expected/actual)
        // or with a single-line `>` blockquote prefix (symptom) that multi-line
        // content could escape. The tool's description tells the calling
        // agent to pass the body to GitHub `create_issue` — i.e. straight into
        // another LLM's context — so anything that survives untreated is an
        // indirect prompt-injection lane.
        //
        // Cap at 16 KB per field (per §5.6 — short user text class). The fence
        // strips HTML comments (the most common smuggle vector in markdown)
        // and instructs the downstream model to treat the content as data.
        var fencedSymptom = LlmFencing.Fence("user-symptom", symptom, maxBytes: 16 * 1024);
        var fencedExpected = string.IsNullOrWhiteSpace(expected)
            ? null
            : LlmFencing.Fence("user-expected-behavior", expected, maxBytes: 16 * 1024);
        var fencedActual = string.IsNullOrWhiteSpace(actual)
            ? null
            : LlmFencing.Fence("user-actual-behavior", actual, maxBytes: 16 * 1024);

        // Strip HTML comments from the symptom before using it in the title /
        // search URL; the fence handles the body insertion separately. The
        // title is short by SuggestTitle's 80-char truncation, so we don't
        // need a full data-fence here — just sanitize the smuggle vector.
        var sanitisedSymptom = LlmFencing.StripHtmlComments(symptom);

        var sb = new StringBuilder();
        sb.AppendLine($"## Title: {SuggestTitle(sanitisedSymptom)}");
        sb.AppendLine();
        sb.AppendLine("> Drafted by `maf-autopilot` `MafDraftIssue`. Review before posting. Strip any private content (file paths, internal identifiers) from snippet/stack trace.");
        sb.AppendLine();
        sb.AppendLine($"### Symptom");
        sb.AppendLine();
        sb.Append(fencedSymptom);
        sb.AppendLine();

        sb.AppendLine("### Environment");
        sb.AppendLine();
        sb.AppendLine("| Component | Version |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| MAF (tracked via `.maf-version`) | `{env.TrackedMafVersion}` |");
        foreach (var (name, version) in env.MafPackages)
            sb.AppendLine($"| `{name}` | `{version}` |");
        sb.AppendLine($"| .NET TFM | `{env.DotnetTfm}` |");
        sb.AppendLine($"| OS | `{env.OsDescription}` |");
        sb.AppendLine();

        sb.AppendLine("### Steps to reproduce");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(snippet))
        {
            // Phase 2.G fixup (Finding 2) — `snippet` sits inside a triple-backtick
            // csharp block, so a snippet containing ``` would escape the fence and
            // any HTML comment inside would inject into the downstream create_issue
            // agent's context. We can't use LlmFencing.Fence here without dropping
            // the markdown code-block (the code-block is part of the issue
            // template that human reviewers expect), so apply the two cheaper
            // defenses: NeutralizeFences breaks any embedded ``` via zero-width
            // space; StripHtmlComments removes the most common smuggle vector.
            var sanitised = RegistryService.NeutralizeFences(
                LlmFencing.StripHtmlComments(snippet.Trim()));
            sb.AppendLine("```csharp");
            sb.AppendLine(sanitised);
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("1. _(Fill in)_");
            sb.AppendLine("2. _(Fill in)_");
            sb.AppendLine("3. _(Fill in)_");
        }
        sb.AppendLine();

        sb.AppendLine("### Expected behaviour");
        sb.AppendLine();
        if (fencedExpected is not null)
            sb.Append(fencedExpected);
        else
            sb.AppendLine("_(Describe what should happen. If you can quote the relevant guide section, do.)_");
        sb.AppendLine();

        sb.AppendLine("### Actual behaviour");
        sb.AppendLine();
        if (fencedActual is not null)
            sb.Append(fencedActual);
        else
            sb.AppendLine("_(Exception type + first stack frame is ideal. Or: \"workflow exits cleanly but produces no output.\")_");
        sb.AppendLine();

        sb.AppendLine("### maf-autopilot interpretation");
        sb.AppendLine();
        if (registryMatches.Count > 0)
        {
            sb.AppendLine("**Registry hits** — patterns the toolkit already knows about. **Filing this issue is probably unnecessary if a hit matches your case** — apply the deterministic fix instead.");
            sb.AppendLine();
            foreach (var entry in registryMatches)
                sb.AppendLine($"- **`{entry.Id}`** — {entry.FixDescription.Replace('\n', ' ').Replace('\r', ' ')}");
            sb.AppendLine();
            sb.AppendLine("If your case differs from these registry entries, proceed with the issue and link the entry IDs in the body so triage can see the toolkit's interpretation.");
        }
        else
        {
            sb.AppendLine("- No registry entries matched the symptom or snippet.");
            sb.AppendLine("- This appears to be a **novel** pattern. After upstream triage, please consider opening a follow-up so we can add a registry entry — that's the `maf-migration-retrospective` skill's job.");
        }
        sb.AppendLine();

        sb.AppendLine("### Filing checklist");
        sb.AppendLine();
        sb.AppendLine("- [ ] I've checked existing issues for duplicates: https://github.com/microsoft/agent-framework/issues?q=" + Uri.EscapeDataString(sanitisedSymptom));
        sb.AppendLine("- [ ] My repro is minimal (no unrelated app code).");
        sb.AppendLine("- [ ] I've stripped private content (file paths, internal identifiers, secrets).");
        sb.AppendLine("- [ ] The stack trace (if any) is the first 10-15 frames, not the full thing.");
        sb.AppendLine();

        sb.AppendLine("### How to post this");
        sb.AppendLine();
        sb.AppendLine("- **Manually:** copy the above and paste into <https://github.com/microsoft/agent-framework/issues/new>.");
        sb.AppendLine("- **Via GitHub MCP server** (if installed): ask Copilot to *\"create an issue in `microsoft/agent-framework` with this body.\"*");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("_Drafted by `maf-autopilot` `MafDraftIssue` on " + DateTime.UtcNow.ToString("yyyy-MM-dd") + ". Toolkit version: see `.maf-version`._");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Pure helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Detects MAF + .NET + OS info from the project repo. Best-effort —
    /// returns "unknown" for anything that can't be confidently inferred.
    /// </summary>
    internal static EnvironmentInfo DetectEnvironment(string repoPath)
    {
        var trackedVersion = ReadFileOrFallback(Path.Combine(repoPath, ".maf-version"), "(not present)");
        var (mafPackages, dotnetTfm) = ScanCsprojFiles(repoPath);
        var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        return new EnvironmentInfo(
            TrackedMafVersion: trackedVersion.Trim(),
            MafPackages: mafPackages,
            DotnetTfm: dotnetTfm,
            OsDescription: osDescription);
    }

    private static string ReadFileOrFallback(string path, string fallback)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : fallback; }
        catch { return fallback; }
    }

    private static readonly Regex PackageRefRegex = new(
        @"<PackageReference\s+Include=""(?<id>[^""]+)""\s+Version=""(?<ver>[^""]+)""",
        RegexOptions.Compiled);
    private static readonly Regex TfmRegex = new(
        @"<TargetFramework[s]?>(?<tfm>[^<]+)</TargetFramework[s]?>",
        RegexOptions.Compiled);

    private static (IReadOnlyList<(string Id, string Version)> MafPkgs, string DotnetTfm) ScanCsprojFiles(string repoPath)
    {
        var mafPkgs = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tfms = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Phase 5.G fixup — was Directory.EnumerateFiles(..., SearchOption.AllDirectories)
            // which followed symlinks by default. Migrated to SourceFileWalker.EnumerateCsprojFiles
            // which uses RecursiveCsOptions (AttributesToSkip = ReparsePoint).
            foreach (var csproj in SourceFileWalker.EnumerateCsprojFiles(repoPath))
            {
                var content = File.ReadAllText(csproj);
                foreach (Match m in PackageRefRegex.Matches(content))
                {
                    var id = m.Groups["id"].Value;
                    if (id.StartsWith("Microsoft.Agents.AI", StringComparison.OrdinalIgnoreCase)
                        || id.StartsWith("Microsoft.Extensions.AI", StringComparison.OrdinalIgnoreCase))
                        mafPkgs[id] = m.Groups["ver"].Value;
                }
                foreach (Match m in TfmRegex.Matches(content))
                    tfms.Add(m.Groups["tfm"].Value);
            }
        }
        catch { /* best-effort */ }
        return (
            mafPkgs.Select(kv => (kv.Key, kv.Value)).ToList(),
            tfms.Count == 0 ? "(not detected)" : string.Join(" / ", tfms)
        );
    }

    /// <summary>
    /// Cross-references the symptom + snippet against the obsolete-API registry.
    /// Returns up to 3 best matches, ordered by haystack hit count.
    /// </summary>
    internal IReadOnlyList<RegistryEntry> MatchRegistry(string symptom, string? snippet)
    {
        var haystack = (symptom + " " + (snippet ?? string.Empty)).ToLowerInvariant();
        var ids = _registry.AllIds;
        var hits = new List<(RegistryEntry Entry, int Score)>();
        foreach (var id in ids)
        {
            var entry = _registry.FindById(id);
            if (entry is null) continue;
            var score = 0;
            // Match on the method name or any quoted-symbol-ish token in the body.
            if (!string.IsNullOrWhiteSpace(entry.Method)
                && haystack.Contains(entry.Method.ToLowerInvariant(), StringComparison.Ordinal))
                score += 3;
            if (!string.IsNullOrWhiteSpace(entry.Type)
                && haystack.Contains(LastSegment(entry.Type).ToLowerInvariant(), StringComparison.Ordinal))
                score += 2;
            if (score > 0) hits.Add((entry, score));
        }
        return hits.OrderByDescending(h => h.Score).Take(3).Select(h => h.Entry).ToList();
    }

    private static string LastSegment(string typeName)
    {
        var i = typeName.LastIndexOf('.');
        return i < 0 ? typeName : typeName[(i + 1)..];
    }

    /// <summary>
    /// Suggests an issue title — strips trailing punctuation, caps the symptom
    /// to ~80 chars so the GitHub title field doesn't truncate.
    /// </summary>
    internal static string SuggestTitle(string symptom)
    {
        var trimmed = symptom.Trim().TrimEnd('.', '!', '?');
        if (trimmed.Length > 80) trimmed = trimmed[..77] + "...";
        return trimmed;
    }

    public sealed record EnvironmentInfo(
        string TrackedMafVersion,
        IReadOnlyList<(string Id, string Version)> MafPackages,
        string DotnetTfm,
        string OsDescription);
}
