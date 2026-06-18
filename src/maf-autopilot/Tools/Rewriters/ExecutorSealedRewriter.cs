using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafDoctor.Tools.Rewriters;

/// <summary>
/// Rule: <c>MAF-AP-WF-001</c>.
///
/// Rewrite: ensure every class deriving from <c>Executor</c> with
/// <c>[MessageHandler]</c> methods carries the <c>sealed</c> modifier.
///
/// Matches the same predicate as <see cref="AntiPatternScannerTool"/>'s
/// MAF-AP-WF-001 rule: derives from <c>Executor</c> (substring match on the
/// base list) AND has at least one method decorated with
/// <c>[MessageHandler]</c>.
/// </summary>
internal sealed class ExecutorSealedRewriter : CSharpSyntaxRewriter, IRuleRewriter
{
    public string RuleId => "MAF-AP-WF-001";

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        if (!IsExecutorWithMessageHandler(node))
            return base.VisitClassDeclaration(node);

        var modifiers = node.Modifiers.Select(m => m.ValueText).ToHashSet();
        var needsSealed = !modifiers.Contains("sealed");
        var needsPartial = !modifiers.Contains("partial");
        if (!needsSealed && !needsPartial)
            return base.VisitClassDeclaration(node); // already `sealed partial`

        // The scanner (MAF-AP-WF-001) fires when EITHER `sealed` or `partial` is
        // missing, and `partial` is the load-bearing one (without it the workflow
        // source generator can't emit the dispatcher → generator-time compile
        // error). So add WHICHEVER is missing — not just `sealed`.

        // Corruption guard: `sealed abstract` / `sealed static` are C# compile
        // errors, so `sealed` can't be added to those. An abstract Executor can
        // never be `sealed partial` at all (abstract precludes sealed) — the
        // scanner skips abstract classes for that reason, so a real WF-001 finding
        // won't reach here — but if autofix-all visits one directly, leave a
        // comment pointing at the real fix (make the concrete subclass
        // `sealed partial`, or don't derive the abstract base from Executor)
        // rather than emit uncompilable code.
        if (needsSealed && modifiers.Contains("abstract"))
        {
            return AddSkipWarning(node,
                "// MAF-AP-WF-001: cannot seal an abstract Executor — refactor to a non-abstract class first.");
        }
        if (needsSealed && modifiers.Contains("static"))
        {
            return AddSkipWarning(node,
                "// MAF-AP-WF-001: cannot seal a static class — static classes cannot derive from Executor anyway; this rule shouldn't be reachable.");
        }

        // Build canonical `[access] sealed … partial class`: insert `sealed` right
        // after the first (access) modifier; append `partial` last (immediately
        // before the `class` keyword). Each inserted token carries a trailing
        // space so the rendered text stays well-formed.
        var list = node.Modifiers.ToList();
        if (needsSealed)
        {
            var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);
            // Insert after the first modifier; if there are none, prepend.
            list.Insert(list.Count == 0 ? 0 : 1, sealedToken);
        }
        if (needsPartial)
        {
            var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);
            list.Add(partialToken); // last modifier, right before `class`
        }

        return node.WithModifiers(SyntaxFactory.TokenList(list));
    }

    /// <summary>
    /// Prepend a leading-trivia comment to the class declaration explaining why the
    /// rewriter cannot apply the fix — the safe alternative to emitting uncompilable
    /// <c>sealed abstract</c>. This DOES change the node, so the caller's
    /// <c>MafAutoFix</c>/<c>MafBeforeAfter</c> flow counts the file as changed and
    /// writes the breadcrumb to disk. That is a deliberate trade-off (surface the
    /// manual-fix guidance) rather than a silent no-op: the scanner skips abstract
    /// Executors, so this path is only reached when autofix visits one directly.
    /// Behaviour is pinned by tests.
    ///
    /// Idempotent — dedup by scanning the class's source text for the same
    /// comment. Roslyn re-parses can shift trivia between adjacent tokens,
    /// so source-text inspection is more reliable than checking just the
    /// node's immediate leading-trivia list.
    /// </summary>
    private static SyntaxNode AddSkipWarning(ClassDeclarationSyntax node, string comment)
    {
        if (node.ToFullString().Contains(comment, StringComparison.Ordinal))
        {
            return node;
        }
        return node.WithLeadingTrivia(
            node.GetLeadingTrivia()
                .Add(SyntaxFactory.Comment(comment))
                .Add(SyntaxFactory.EndOfLine("\n")));
    }

    private static bool IsExecutorWithMessageHandler(ClassDeclarationSyntax cls)
    {
        // Shared predicate with the WF-001 scanner rule — matches plain `Executor`,
        // generic `Executor<…>`, and qualified forms — so detector and rewriter stay
        // in lockstep (no "flagged as fixable but silently not rewritten" parity gap).
        var derivesFromExecutor = cls.BaseList?.Types
            .Any(t => AntiPatternScannerTool.IsExecutorBaseType(t.Type)) ?? false;
        if (!derivesFromExecutor) return false;

        return cls.Members.OfType<MethodDeclarationSyntax>().Any(m =>
            m.AttributeLists.SelectMany(a => a.Attributes).Any(a =>
            {
                var n = a.Name.ToString();
                var s = n.Contains('.') ? n[(n.LastIndexOf('.') + 1)..] : n;
                return s is "MessageHandler" or "MessageHandlerAttribute";
            }));
    }
}
