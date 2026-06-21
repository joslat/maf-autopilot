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

        // Parity with the WF-001 scanner, which SKIPS abstract Executors entirely:
        // an abstract class can't be `sealed` (the two modifiers are mutually
        // exclusive), and a static class can't derive from Executor at all. Leave
        // both UNTOUCHED — returning the node unchanged means MafAutoFix /
        // MafBeforeAfter honestly report "no change" for a file the scanner also
        // considers clean, instead of dirtying it with an advisory comment. (The
        // concrete subclass is what must be `sealed partial`; the scanner flags THAT.)
        if (modifiers.Contains("abstract") || modifiers.Contains("static"))
            return base.VisitClassDeclaration(node);

        var needsSealed = !modifiers.Contains("sealed");
        var needsPartial = !modifiers.Contains("partial");
        if (!needsSealed && !needsPartial)
            return base.VisitClassDeclaration(node); // already `sealed partial`

        // The scanner (MAF-AP-WF-001) fires when EITHER `sealed` or `partial` is
        // missing, and `partial` is the load-bearing one (without it the workflow
        // source generator can't emit the dispatcher → generator-time compile
        // error). So add WHICHEVER is missing — not just `sealed`.

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
