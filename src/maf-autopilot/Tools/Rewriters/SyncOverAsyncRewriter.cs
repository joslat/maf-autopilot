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
/// walks ancestors and skips with a TODO comment in every position where the
/// C# spec makes <c>await</c> illegal (otherwise the rewrite emits uncompilable
/// source). These are the finite await-illegal contexts:</para>
/// <list type="bullet">
///   <item>Inside a <c>lock</c> block (CS1996).</item>
///   <item>Inside a LINQ query clause other than the initial <c>from</c> /
///         <c>join … in</c> source (CS1995).</item>
///   <item>Inside a <c>catch when (...)</c> filter expression (CS7094) — the
///         catch body itself remains rewritable.</item>
///   <item>Inside an unsafe context (CS4004): an <c>unsafe</c>/<c>fixed</c>
///         block, or under the <c>unsafe</c> modifier on the enclosing
///         method / local function / type.</item>
///   <item>Outside any <c>async</c> function — the enclosing method / local
///         function / lambda must be <c>async</c> (whitelist walk), else the
///         <c>.Result</c> sits in a context that can never be async
///         (property / field / operator / primary-ctor base / …) (CS4032/CS4033).</item>
/// </list>
/// Pre-fix the rewriter happily inserted <c>await</c> in these cases and
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

        // (a2) LINQ query clause (CS1995): `await` is legal ONLY in the initial
        // `from` source and a `join … in` source — never in let/where/select/
        // orderby/group/secondary-from. Skip those (default-safe direction).
        var query = node.FirstAncestorOrSelf<QueryExpressionSyntax>();
        if (query is not null && !IsInAwaitableQuerySource(node, query))
        {
            reason = "// MAF-AP-CONC-002: cannot await inside this query clause — materialize the awaited value before the query.";
            return true;
        }

        // (a3) Catch filter (CS7094): `await` is illegal in a `catch when (...)`
        // filter expression. The catch BODY is still awaitable, so we guard the
        // filter clause specifically (FirstAncestorOrSelf<CatchFilterClauseSyntax>
        // matches only the `when (...)` expression, never the handler block).
        if (node.FirstAncestorOrSelf<CatchFilterClauseSyntax>() is not null)
        {
            reason = "// MAF-AP-CONC-002: cannot await inside a catch filter (`when (...)`) — hoist the awaited value into a variable before the try/catch.";
            return true;
        }

        // (a4) Unsafe context (CS4004): `await` is illegal wherever an unsafe
        // context is in effect — inside an `unsafe { }` or `fixed ( )` block, OR
        // under the `unsafe` modifier on the enclosing method / local function /
        // any enclosing type (the modifier makes the whole declaration unsafe,
        // including nested types and members).
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is UnsafeStatementSyntax or FixedStatementSyntax
                || (ancestor is MethodDeclarationSyntax um && um.Modifiers.Any(mod => mod.IsKind(SyntaxKind.UnsafeKeyword)))
                || (ancestor is LocalFunctionStatementSyntax ulf && ulf.Modifiers.Any(mod => mod.IsKind(SyntaxKind.UnsafeKeyword)))
                || (ancestor is BaseTypeDeclarationSyntax ut && ut.Modifiers.Any(mod => mod.IsKind(SyntaxKind.UnsafeKeyword))))
            {
                reason = "// MAF-AP-CONC-002: cannot await in an unsafe context — move the awaited call out of the `unsafe`/`fixed` scope or drop the `unsafe` modifier.";
                return true;
            }
        }

        // (b) WHITELIST, not blacklist. Walk outward and rewrite ONLY when we
        // positively land in an async-capable scope: an `async` method / local
        // function / lambda, or a top-level statement (the generated entry point is
        // async). If we instead reach a type-declaration body or the compilation-unit
        // root WITHOUT finding one, the `.Result` sits in a context that can never be
        // `async` (property / field / operator / conversion / primary-ctor base /
        // attribute / …), so we SKIP. Defaulting to skip closes the entire
        // await-into-non-async corruption class in one place, instead of trying to
        // enumerate every non-async member kind (we kept missing some — operators,
        // then C# 12 primary-constructor base initializers).
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax m:
                    if (m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword))) { reason = string.Empty; return false; }
                    reason = "// MAF-AP-CONC-002: enclosing method is not async — mark `async` (and adjust return type) or refactor before applying this rule.";
                    return true;
                case LocalFunctionStatementSyntax lf:
                    if (lf.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword))) { reason = string.Empty; return false; }
                    reason = "// MAF-AP-CONC-002: enclosing local function is not async — mark `async` or refactor.";
                    return true;
                case AnonymousFunctionExpressionSyntax af:
                    if (af.AsyncKeyword != default) { reason = string.Empty; return false; }
                    reason = "// MAF-AP-CONC-002: enclosing lambda is not async — add `async` or refactor.";
                    return true;
                case GlobalStatementSyntax:
                    // Top-level statement — the generated Main is async-capable.
                    reason = string.Empty;
                    return false;
                case BaseTypeDeclarationSyntax: // class/struct/record/interface/enum body reached without an async fn
                case CompilationUnitSyntax:     // reached the root without an async fn
                    reason = "// MAF-AP-CONC-002: this position cannot `await` (not inside an async method/function) — refactor the call site, e.g. make it an async method.";
                    return true;
            }
        }

        // Defensive default: never inject `await` into an unrecognized context.
        reason = "// MAF-AP-CONC-002: cannot await here — no enclosing async context.";
        return true;
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
    /// <summary>True only when the node sits in an await-legal query position: the
    /// query's initial <c>from</c> source, or a <c>join … in</c> source expression.</summary>
    private static bool IsInAwaitableQuerySource(SyntaxNode node, QueryExpressionSyntax query)
    {
        if (query.FromClause.Expression.Span.Contains(node.Span)) return true;
        foreach (var join in query.DescendantNodes().OfType<JoinClauseSyntax>())
            if (join.InExpression.Span.Contains(node.Span)) return true;
        return false;
    }

    private static SyntaxNode WithTodoTrivia(SyntaxNode node, string commentText)
    {
        // Dedup scope: the enclosing function, else the enclosing TYPE declaration
        // (covers contexts with no method ancestor — primary-ctor base initializers,
        // field / collection-expression initializers — which otherwise re-stacked a
        // duplicate comment on a second pass), else the parent.
        var enclosingMethod = (SyntaxNode?)node.FirstAncestorOrSelf<MethodDeclarationSyntax>()
            ?? node.FirstAncestorOrSelf<LocalFunctionStatementSyntax>()
            ?? node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>()
            ?? node.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>()
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
