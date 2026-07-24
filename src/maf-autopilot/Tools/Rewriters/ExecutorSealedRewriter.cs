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
            // `partial` MUST stay immediately before `class` (CS0267), so `sealed`
            // has to be inserted BEFORE any existing `partial`. Insert right at the
            // first `partial` if present; otherwise after the access modifier
            // (index 1), or at the front when there are no modifiers at all.
            // Without this, a `partial`-first declaration (`partial class X`, the
            // idiomatic internal-by-default shape) was rewritten to the uncompilable
            // `partial sealed class X`.
            var partialIdx = list.FindIndex(t => t.IsKind(SyntaxKind.PartialKeyword));
            var insertAt = partialIdx >= 0 ? partialIdx : Math.Min(1, list.Count);
            if (insertAt == 0 && list.Count > 0)
            {
                // `sealed` becomes the new first MODIFIER — carry the old first
                // modifier's leading trivia (indentation / xmldoc) onto it.
                sealedToken = sealedToken.WithLeadingTrivia(list[0].LeadingTrivia);
                list[0] = list[0].WithLeadingTrivia();
            }
            else if (insertAt == 0 && node.AttributeLists.Count == 0)
            {
                // No modifiers AND no attributes: `sealed` becomes the first token of
                // the whole declaration. Carry the `class` keyword's leading trivia
                // (indentation + any `///` doc) onto `sealed`, else it strands between
                // the modifiers and `class` — de-indenting the line and DETACHING the
                // XML doc from the type (CS1587, documentation lost).
                sealedToken = sealedToken.WithLeadingTrivia(node.Keyword.LeadingTrivia);
                node = node.WithKeyword(node.Keyword.WithLeadingTrivia());
            }
            list.Insert(insertAt, sealedToken);
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
