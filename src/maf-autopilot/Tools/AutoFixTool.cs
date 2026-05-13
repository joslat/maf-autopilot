using System.ComponentModel;
using System.Text.Json;
using MafAutopilot.Tools.Rewriters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Server;

namespace MafAutopilot.Tools;

/// <summary>
/// MCP tool: <c>MafAutoFix</c>.
///
/// **The missing "do" half of the toolkit.** Today the detection tools
/// (<see cref="AntiPatternScannerTool"/>, <see cref="FanOutValidatorTool"/>,
/// <see cref="Cs0618HuntTool"/>) report findings; the migration agent (in
/// Copilot Chat) applies the fixes. <c>MafAutoFix</c> closes that loop for
/// the mechanical rules — no LLM call, no judgement, just deterministic
/// Roslyn syntax rewriting.
///
/// **Supported rules** (by rule ID, with aliases):
/// <list type="bullet">
///   <item><c>MAF-AP-SEC-001</c> / <c>MAF002</c> — DefaultAzureCredential → ManagedIdentityCredential</item>
///   <item><c>MAF-AP-SEC-003</c> / <c>MAF003</c> — drop <c>EnableSensitiveData = true</c></item>
///   <item><c>MAF-AP-WF-001</c> — add missing <c>sealed</c> to Executor classes</item>
///   <item><c>MAF130-FAN-IN-001</c> — swap <c>AddFanInBarrierEdge</c> arg order</item>
///   <item><c>MAF-AP-CONC-002</c> — <c>.Result</c> → <c>(await ...)</c>; <c>.Wait()</c> → <c>await ...</c></item>
/// </list>
///
/// See <c>src/maf-autopilot/Tools/Rewriters/IRuleRewriter.cs</c> for the
/// rewriter contract; each rule is one <see cref="CSharpSyntaxRewriter"/>
/// in <c>Rewriters/</c>.
/// </summary>
[McpServerToolType]
public sealed class AutoFixTool
{
    // ruleId -> factory. Two IDs may share a rewriter (e.g. MAF-AP-SEC-001 ≡ MAF002).
    private static readonly IReadOnlyDictionary<string, Func<IRuleRewriter>> _factories =
        new Dictionary<string, Func<IRuleRewriter>>(StringComparer.OrdinalIgnoreCase)
        {
            ["MAF-AP-SEC-001"]      = () => new DefaultAzureCredentialRewriter(),
            ["MAF002"]              = () => new DefaultAzureCredentialRewriter(),
            ["MAF-AP-SEC-003"]      = () => new EnableSensitiveDataRewriter(),
            ["MAF003"]              = () => new EnableSensitiveDataRewriter(),
            ["MAF-AP-WF-001"]       = () => new ExecutorSealedRewriter(),
            ["MAF130-FAN-IN-001"]   = () => new FanInArgOrderRewriter(),
            ["MAF-AP-CONC-002"]     = () => new SyncOverAsyncRewriter(),
        };

    /// <summary>The set of rule IDs this tool can auto-fix. Read-only.</summary>
    public static IReadOnlyCollection<string> SupportedRuleIds => _factories.Keys.ToList();

    /// <summary>
    /// Internal helper for sibling tools (e.g. <see cref="BeforeAfterTool"/>)
    /// to share the rewriter registry without re-declaring it.
    /// </summary>
    internal static bool TryCreateRewriter(string ruleId, out IRuleRewriter? rewriter)
    {
        if (_factories.TryGetValue(ruleId, out var factory))
        {
            rewriter = factory();
            return true;
        }
        rewriter = null;
        return false;
    }

    [McpServerTool]
    [Description("""
        Deterministically apply the fix for a single registry rule across a codebase.

        Supported ruleIds: MAF-AP-SEC-001 / MAF002 (DefaultAzureCredential),
        MAF-AP-SEC-003 / MAF003 (EnableSensitiveData=true), MAF-AP-WF-001 (sealed
        modifier on Executor), MAF130-FAN-IN-001 (fan-in arg order),
        MAF-AP-CONC-002 (.Result / .Wait()).

        Input:
          - repoPath: absolute path to the repo or project to fix.
          - ruleId: which rule's auto-fixer to run.
          - specificFile: optional. Limit fixes to a single file (path relative to repoPath).
          - dryRun: if true, no files are written — just returns the JSON summary
            of what WOULD change. Default false (writes are applied).

        Returns a JSON document with `ruleId`, `dryRun`, `filesChanged` count,
        `changedFiles` list, and any per-file `error` strings.

        Bad ruleId or empty repoPath returns an error JSON.
        """)]
    public string MafAutoFix(
        [Description("Absolute path to the repo or project to fix.")] string repoPath,
        [Description("Rule ID, e.g. 'MAF-AP-SEC-001' or 'MAF130-FAN-IN-001'.")] string ruleId,
        [Description("Optional: relative path to limit fixes to a single file.")] string? specificFile = null,
        [Description("If true, no files written — just preview.")] bool dryRun = false)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err)
            return JsonSerializer.Serialize(new { error = err });

        if (!_factories.TryGetValue(ruleId, out var factory))
            return JsonSerializer.Serialize(new
            {
                error = $"Unknown ruleId '{ruleId}'. Supported: {string.Join(", ", SupportedRuleIds)}",
            });

        var result = ApplyRewriterToRepo(factory(), repoPath, specificFile, dryRun);
        return JsonSerializer.Serialize(new
        {
            ruleId,
            dryRun,
            filesChanged = result.ChangedFiles.Count,
            changedFiles = result.ChangedFiles,
            errors = result.Errors,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("""
        Apply EVERY supported rewriter across the codebase in dependency-safe
        order — the "fix everything fixable" command. Use this when running
        a clean migration pass instead of cherry-picking individual rules.

        Order of execution (each runs over the output of the previous):
          1. MAF-AP-WF-001       — add `sealed` modifier (purely additive)
          2. MAF-AP-SEC-003      — drop EnableSensitiveData = true
          3. MAF-AP-SEC-001      — DefaultAzureCredential → ManagedIdentityCredential
          4. MAF130-FAN-IN-001   — swap fan-in arg order
          5. MAF-AP-CONC-002     — .Result / .Wait() → await

        Input:
          - repoPath: absolute path to the repo or project to fix.
          - specificFile: optional, relative path to limit fixes to a single file.
          - dryRun: if true, no files are written — just returns the JSON
            summary of what WOULD change. Default false.

        Returns aggregate JSON with the ordered rule list + total distinct files
        changed (union across all rules) + per-rule breakdown.
        """)]
    public string MafAutoFixAll(
        [Description("Absolute path to the repo or project to fix.")] string repoPath,
        [Description("Optional: relative path to limit fixes to a single file.")] string? specificFile = null,
        [Description("If true, no files written — just preview.")] bool dryRun = false)
    {
        if (PathGuard.ValidateRepoPath(repoPath) is { } err)
            return JsonSerializer.Serialize(new { error = err });

        // **Dependency-safe execution order.** Rationale per rule:
        //   1. ExecutorSealedRewriter      — purely additive modifier; never affects other rewrites.
        //   2. EnableSensitiveDataRewriter — removes an initializer-list entry; doesn't affect rules 3-5.
        //   3. DefaultAzureCredentialRewriter — type-name swap; independent of other rules.
        //   4. FanInArgOrderRewriter       — argument-position swap on a specific invocation; runs
        //      AFTER 1-3 in case any of them happen to alter the surrounding context.
        //   5. SyncOverAsyncRewriter       — wraps an invocation in await; runs LAST in case rules
        //      3-4 changed the invocation's identity (they don't today, but order-safety > order-speed).
        //
        // Aliases (MAF002 / MAF003) deliberately NOT in this list — we want each rewriter to run
        // EXACTLY ONCE per pass. The aliased IDs route to the same factory.
        var orderedRules = new[]
        {
            "MAF-AP-WF-001",
            "MAF-AP-SEC-003",
            "MAF-AP-SEC-001",
            "MAF130-FAN-IN-001",
            "MAF-AP-CONC-002",
        };

        var perRule = new Dictionary<string, object>();
        var allChangedFiles = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var ruleId in orderedRules)
        {
            if (!_factories.TryGetValue(ruleId, out var factory)) continue;
            var result = ApplyRewriterToRepo(factory(), repoPath, specificFile, dryRun);
            perRule[ruleId] = new
            {
                filesChanged = result.ChangedFiles.Count,
                changedFiles = result.ChangedFiles,
            };
            foreach (var f in result.ChangedFiles) allChangedFiles.Add(f);
        }

        return JsonSerializer.Serialize(new
        {
            dryRun,
            orderOfExecution = orderedRules,
            totalDistinctFilesChanged = allChangedFiles.Count,
            affectedFiles = allChangedFiles.ToList(),
            perRule,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Per-rule run result. Returned by <see cref="ApplyRewriterToRepo"/> and
    /// shared between <see cref="MafAutoFix"/> and <see cref="MafAutoFixAll"/>.
    /// </summary>
    private sealed record RewriterRunResult(IReadOnlyList<string> ChangedFiles, IReadOnlyList<object> Errors);

    /// <summary>
    /// Walks every .cs file (or just one), applies the rewriter, and writes
    /// changed files atomically. Pure helper — no JSON, no PathGuard (caller
    /// has already validated).
    /// </summary>
    private static RewriterRunResult ApplyRewriterToRepo(
        IRuleRewriter rewriter, string repoPath, string? specificFile, bool dryRun)
    {
        var changedFiles = new List<string>();
        var errors = new List<object>();

        foreach (var file in EnumerateFiles(repoPath, specificFile))
        {
            try
            {
                var src = File.ReadAllText(file);
                var tree = CSharpSyntaxTree.ParseText(src);
                var oldRoot = tree.GetRoot();
                var newRoot = rewriter.Visit(oldRoot);

                if (ReferenceEquals(newRoot, oldRoot)) continue;

                if (!dryRun)
                    WriteAtomic(file, newRoot.ToFullString());

                changedFiles.Add(SourceFileWalker.MakeRelative(repoPath, file));
            }
            catch (Exception ex)
            {
                errors.Add(new
                {
                    file = SourceFileWalker.MakeRelative(repoPath, file),
                    error = ex.Message,
                });
            }
        }
        return new RewriterRunResult(changedFiles, errors);
    }

    /// <summary>
    /// Files to scan. If <paramref name="specificFile"/> is set, restrict to
    /// that single file (resolved relative to <paramref name="repoPath"/>).
    /// </summary>
    private static IEnumerable<string> EnumerateFiles(string repoPath, string? specificFile)
    {
        if (string.IsNullOrEmpty(specificFile))
            return SourceFileWalker.EnumerateCsFiles(repoPath);

        var resolved = Path.IsPathRooted(specificFile)
            ? specificFile
            : Path.Combine(repoPath, specificFile);

        return File.Exists(resolved) ? new[] { resolved } : Array.Empty<string>();
    }

    /// <summary>
    /// Atomic file write: stage to a temp file beside the target, then move.
    /// Avoids torn writes if the process is killed mid-rewrite.
    /// </summary>
    private static void WriteAtomic(string path, string contents)
    {
        var tmp = path + ".autofix.tmp";
        File.WriteAllText(tmp, contents);
        File.Move(tmp, path, overwrite: true);
    }
}
