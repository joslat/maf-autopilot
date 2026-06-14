using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafAutopilot.Tools.Rewriters;

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
        if (modifiers.Contains("sealed"))
            return base.VisitClassDeclaration(node); // already sealed

        // Phase 4.1a — corruption guard: `sealed abstract class` is a C#
        // compile error (the two modifiers are mutually exclusive). Same for
        // `sealed static class` (static implies sealed AND abstract; combining
        // with explicit `sealed` is also rejected). Pre-fix, the rewriter
        // happily inserted `sealed` regardless and produced uncompilable user
        // code. Now: skip the rewrite and leave a comment-warning so the
        // developer sees why the rule wasn't auto-applied.
        if (modifiers.Contains("abstract"))
        {
            return AddSkipWarning(node,
                "// MAF-AP-WF-001: cannot seal an abstract Executor — refactor to a non-abstract class first.");
        }
        if (modifiers.Contains("static"))
        {
            return AddSkipWarning(node,
                "// MAF-AP-WF-001: cannot seal a static class — static classes cannot derive from Executor anyway; this rule shouldn't be reachable.");
        }

        // Insert `sealed` immediately after `public` (or any first access modifier);
        // fall back to the start of the modifier list. Preserve trivia by borrowing
        // the trailing trivia of the preceding token.
        var sealedToken = SyntaxFactory.Token(SyntaxKind.SealedKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);

        SyntaxTokenList newModifiers;
        if (node.Modifiers.Count == 0)
        {
            newModifiers = SyntaxFactory.TokenList(sealedToken);
        }
        else
        {
            // Insert after the first modifier (preserves "public sealed partial class X").
            // Note: when `node.Modifiers.Count == 1` (e.g. just `public`), index 1
            // equals the list's Count, and List<T>.Insert(Count, x) appends — which
            // is exactly what we want: `public` → `public sealed`.
            var list = node.Modifiers.ToList();
            list.Insert(1, sealedToken);
            newModifiers = SyntaxFactory.TokenList(list);
        }

        return node.WithModifiers(newModifiers);
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
