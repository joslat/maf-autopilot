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

        // Phase 4.1a — corruption guard: `sealed abstract` / `sealed static` are
        // C# compile errors. We can't add `sealed` to those, so skip with a
        // comment-warning rather than emit uncompilable code. (Adding `sealed`
        // is what's blocked; an abstract class missing `partial` is a refactor
        // the developer must do anyway.)
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
    /// Attach a leading-trivia comment to the class declaration explaining why
    /// the rewriter skipped it. Returns the node unchanged otherwise — the
    /// caller's `MafAutoFix` flow will record "no change" for this file, which
    /// is the right answer: we'd rather emit nothing than emit garbage.
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
        var derivesFromExecutor = cls.BaseList?.Types
            .Select(t => t.Type.ToString())
            .Any(b => b == "Executor" || b.EndsWith(".Executor", StringComparison.Ordinal)) ?? false;
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
