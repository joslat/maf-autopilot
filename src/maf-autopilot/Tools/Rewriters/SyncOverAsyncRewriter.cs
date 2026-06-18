using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafDoctor.Tools.Rewriters;

/// <summary>
/// Rule: <c>MAF-AP-CONC-002</c>.
///
/// Rewrite: <c>SomeAsync().Result</c> → <c>(await SomeAsync())</c>.
/// Also handles <c>SomeAsync().Wait()</c> → <c>await SomeAsync()</c>.
///
/// <para><b>Corruption guard (Phase 4.1b).</b> Before rewriting, the visitor
/// walks ancestors. Skip with a TODO comment if:</para>
/// <list type="bullet">
///   <item>Any ancestor is a <c>lock</c> block — <c>await</c> inside <c>lock</c>
///         is a C# compile error.</item>
///   <item>The enclosing function (method / local function / lambda) is not
///         marked <c>async</c> — <c>await</c> in a synchronous body is also
///         a compile error.</item>
/// </list>
/// Pre-fix the rewriter happily inserted <c>await</c> in either case and
/// produced uncompilable user code. Now we surface a comment and leave the
/// expression unchanged so the developer can refactor manually.
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
            if (ShouldSkipForContext(node, out var reason))
                return WithTodoTrivia(node, reason);

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
            if (ShouldSkipForContext(node, out var reason))
                return WithTodoTrivia(node, reason);

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

    /// <summary>
    /// Returns true if the rewrite must be skipped because awaiting in this
    /// position would produce uncompilable code. Populates <paramref name="reason"/>
    /// with a short comment explaining the skip.
    /// </summary>
    private static bool ShouldSkipForContext(SyntaxNode node, out string reason)
    {
        // (a) Any ancestor lock-statement makes `await` here a CS1996.
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is LockStatementSyntax)
            {
                reason = "// MAF-AP-CONC-002: cannot await inside a lock block — refactor to SemaphoreSlim.WaitAsync or restructure.";
                return true;
            }
        }

        // (b) Enclosing function must be async. Walk to the nearest "function"-y
        // ancestor and check its modifiers / async lambda token. If we hit the
        // top-level program or a property accessor (rare), treat as non-async.
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax m:
                    if (!m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)))
                    {
                        reason = "// MAF-AP-CONC-002: enclosing method is not async — mark `async` (and adjust return type) or refactor before applying this rule.";
                        return true;
                    }
                    reason = string.Empty;
                    return false; // method is async — OK to rewrite
                case LocalFunctionStatementSyntax lf:
                    if (!lf.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)))
                    {
                        reason = "// MAF-AP-CONC-002: enclosing local function is not async — mark `async` or refactor.";
                        return true;
                    }
                    reason = string.Empty;
                    return false;
                case AnonymousFunctionExpressionSyntax af:
                    if (af.AsyncKeyword == default)
                    {
                        reason = "// MAF-AP-CONC-002: enclosing lambda is not async — add `async` or refactor.";
                        return true;
                    }
                    reason = string.Empty;
                    return false;
                case AccessorDeclarationSyntax: // property/event accessors — never async-able in our targets
                    reason = "// MAF-AP-CONC-002: cannot await inside a property/event accessor — refactor.";
                    return true;
                // Expression-bodied / initializer member boundaries that have NO
                // accessor node and can NEVER be `async`. Without these, a `.Result`
                // in `string P => Foo().Result;`, `this[int i] => …`, a field
                // initializer, or a ctor/dtor fell through to the async-allowed
                // default and got `await` injected → uncompilable (CS4032). These
                // are members (never nested), so they don't shadow the method /
                // local-function / lambda cases for ordinary bodies.
                case PropertyDeclarationSyntax:
                case IndexerDeclarationSyntax:
                case EventDeclarationSyntax:
                case FieldDeclarationSyntax:
                case ConstructorDeclarationSyntax:
                case DestructorDeclarationSyntax:
                case OperatorDeclarationSyntax:           // operators can never be async
                case ConversionOperatorDeclarationSyntax: // user-defined conversions can never be async
                    reason = "// MAF-AP-CONC-002: cannot await here — this member cannot be `async`; refactor the call site (e.g. make it a method).";
                    return true;
            }
        }

        // No enclosing function found (top-level statement context). Top-level
        // is implicitly async-allowed in modern C#; leave the rewrite alone.
        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Attach a leading-trivia comment to the expression node. Returns the
    /// unchanged node otherwise.
    ///
    /// Idempotence: dedup by scanning the enclosing method / function for
    /// the same comment text. Roslyn re-parses can reattach trivia to
    /// neighboring tokens, so we cannot rely on the node's own leading-trivia
    /// list. Scanning by source-text of the enclosing scope is stable
    /// across parse round-trips.
    /// </summary>
    private static SyntaxNode WithTodoTrivia(SyntaxNode node, string commentText)
    {
        var enclosingMethod = (SyntaxNode?)node.FirstAncestorOrSelf<MethodDeclarationSyntax>()
            ?? node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>()
            ?? node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>()
            ?? node.Parent;
        if (enclosingMethod is not null
            && enclosingMethod.ToFullString().Contains(commentText, StringComparison.Ordinal))
        {
            return node;
        }
        return node.WithLeadingTrivia(
            node.GetLeadingTrivia()
                .Add(SyntaxFactory.Comment(commentText))
                .Add(SyntaxFactory.EndOfLine("\n")));
    }
}
