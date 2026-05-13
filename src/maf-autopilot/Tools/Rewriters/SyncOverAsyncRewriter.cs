using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafAutopilot.Tools.Rewriters;

/// <summary>
/// Rule: <c>MAF-AP-CONC-002</c>.
///
/// Rewrite: <c>SomeAsync().Result</c> → <c>(await SomeAsync())</c>.
/// Also handles <c>SomeAsync().Wait()</c> → <c>await SomeAsync()</c>.
///
/// **Important limitation.** This rewriter does NOT verify the enclosing
/// method is `async`. If it isn't, the produced code won't compile — but
/// the build error is loud and easy to spot. v2 of this rewriter could walk
/// up to the enclosing method declaration and add the <c>async</c> modifier
/// + adjust the return type. For now, the caller (MafAutoFix) emits a TODO
/// comment when changes are made in a non-async method.
/// </summary>
internal sealed class SyncOverAsyncRewriter : CSharpSyntaxRewriter, IRuleRewriter
{
    public string RuleId => "MAF-AP-CONC-002";

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Pattern: <invocation>.Result
        if (node.Name.Identifier.ValueText == "Result"
            && node.Expression is InvocationExpressionSyntax inv)
        {
            // Rewrite `Foo().Result` → `(await Foo())`.
            // Wrap in parens so the surrounding member-access / arg context
            // doesn't bind incorrectly. Explicit `await ` keyword token with a
            // trailing space — otherwise the rewrite produces `awaitFoo()`.
            var awaited = SyntaxFactory.AwaitExpression(
                SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                inv.WithoutLeadingTrivia());
            return SyntaxFactory.ParenthesizedExpression(awaited)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
        return base.VisitMemberAccessExpression(node);
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Pattern: <invocation>.Wait()
        if (node.Expression is MemberAccessExpressionSyntax mae
            && mae.Name.Identifier.ValueText == "Wait"
            && mae.Expression is InvocationExpressionSyntax inner
            && node.ArgumentList.Arguments.Count == 0)
        {
            // Rewrite `Foo().Wait()` → `await Foo()`. Need an explicit
            // trailing-space on the `await` token.
            return SyntaxFactory.AwaitExpression(
                    SyntaxFactory.Token(SyntaxKind.AwaitKeyword).WithTrailingTrivia(SyntaxFactory.Space),
                    inner.WithoutLeadingTrivia())
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
        return base.VisitInvocationExpression(node);
    }
}
