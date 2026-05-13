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

        var rewriter = factory();
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

        return JsonSerializer.Serialize(new
        {
            ruleId,
            dryRun,
            filesChanged = changedFiles.Count,
            changedFiles,
            errors,
        }, new JsonSerializerOptions { WriteIndented = true });
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
