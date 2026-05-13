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
            var list = node.Modifiers.ToList();
            list.Insert(1, sealedToken);
            newModifiers = SyntaxFactory.TokenList(list);
        }

        return node.WithModifiers(newModifiers);
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
