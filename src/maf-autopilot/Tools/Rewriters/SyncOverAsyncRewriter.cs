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

    private const string NonAwaitableReason =
        "// MAF-AP-CONC-002: cannot verify the awaited call returns a Task (no semantic model) — review manually. If it does, give the method an 'Async' suffix or await it explicitly.";

    // Well-known Task / ValueTask static factory methods whose result is awaitable.
    private static readonly HashSet<string> TaskFactoryMethods = new(StringComparer.Ordinal)
    {
        "Run", "FromResult", "FromCanceled", "FromException", "WhenAll", "WhenAny", "WhenEach", "Delay",
    };

    /// <summary>
    /// Syntax-only heuristic (this rewriter has no semantic model) for whether
    /// <paramref name="inv"/> is likely to return an awaitable (Task/ValueTask).
    /// We rewrite ONLY when the receiver is syntactically recognizable as a task
    /// source — the invoked method's simple name ends in <c>Async</c>, or it is a
    /// well-known <c>Task</c>/<c>ValueTask</c> factory (<c>Task.Run</c>,
    /// <c>Task.WhenAll</c>, …). Everything else is left untouched with a
    /// review-manually TODO: a safe skip always beats corrupting source by awaiting
    /// a non-Task (e.g. <c>SemaphoreSlim.Wait()</c> or a custom <c>.Result</c>).
    /// </summary>
    private static bool LooksAwaitableReceiver(InvocationExpressionSyntax inv)
    {
        var name = InvokedSimpleName(inv);
        if (name is null) return false;
        if (name.EndsWith("Async", StringComparison.Ordinal)) return true;
        return inv.Expression is MemberAccessExpressionSyntax ma
            && TaskFactoryMethods.Contains(name)
            && ReceiverTypeName(ma.Expression) is "Task" or "ValueTask";
    }

    private static string? InvokedSimpleName(InvocationExpressionSyntax inv) => inv.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax gn => gn.Identifier.ValueText,
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax mb => mb.Name.Identifier.ValueText,
        _ => null,
    };

    // Rightmost identifier of a factory receiver: `Task`, `System.Threading.Tasks.Task` → "Task".
    private static string? ReceiverTypeName(ExpressionSyntax expr) => expr switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        GenericNameSyntax gn => gn.Identifier.ValueText,
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText,
        _ => null,
    };

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // Pattern: <invocation>.Result
        if (node.Name.Identifier.ValueText == "Result"
            && node.Expression is InvocationExpressionSyntax inv)
        {
            if (ShouldSkipForContext(node, out var reason))
                return WithTodoTrivia(node, reason);

            // Syntax-only awaitability gate: with no semantic model we cannot tell a
            // Task-returning invocation from `GetLock()` (SemaphoreSlim) or a custom
            // `.Result` wrapper — rewriting the latter to `await …` emits uncompilable
            // code (CS1061). Rewrite only when the receiver is syntactically a task
            // source (see LooksAwaitableReceiver); otherwise leave it with a TODO.
            if (!LooksAwaitableReceiver(inv))
                return WithTodoTrivia(node, NonAwaitableReason);

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

            // Same syntax-only awaitability gate as the `.Result` path: `_sem.Wait()`
            // is not matched (receiver is not an invocation), but `GetLock().Wait()`
            // on a SemaphoreSlim IS — and awaiting it would not compile. Skip unless
            // the receiver is syntactically a task source.
            if (!LooksAwaitableReceiver(inner))
                return WithTodoTrivia(node, NonAwaitableReason);

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
        // (UNBOUNDED) Unsafe context (CS4004): `await` is illegal wherever an
        // unsafe context is in effect — inside an `unsafe { }` or `fixed ( )` block,
        // OR under the `unsafe` modifier on the enclosing method / local function /
        // any enclosing type. Unlike lock/query/catch-filter below, an unsafe
        // context PROPAGATES lexically into nested async lambdas, so it is checked
        // across ALL ancestors here, before the bounded walk.
        foreach (var ancestor in node.Ancestors())
        {
            // `MemberDeclarationSyntax` is the common base for method / property /
            // indexer / operator / conversion / constructor / event / field / type
            // declarations — every member kind that can carry the `unsafe` modifier.
            // (Local functions are statements, not members, so they need their own
            // check.) The modifier establishes an unsafe context that propagates
            // lexically into nested async lambdas, so one uniform check here closes
            // the whole class — enumerating only method/localfn/type previously missed
            // `unsafe` properties / indexers / operators / conversions / constructors.
            if (ancestor is UnsafeStatementSyntax or FixedStatementSyntax
                || (ancestor is MemberDeclarationSyntax umd && umd.Modifiers.Any(mod => mod.IsKind(SyntaxKind.UnsafeKeyword)))
                || (ancestor is LocalFunctionStatementSyntax ulf && ulf.Modifiers.Any(mod => mod.IsKind(SyntaxKind.UnsafeKeyword))))
            {
                reason = "// MAF-AP-CONC-002: cannot await in an unsafe context — move the awaited call out of the `unsafe`/`fixed` scope or drop the `unsafe` modifier.";
                return true;
            }
        }

        // SINGLE BOUNDED WALK — walk outward, FIRST match wins. This unifies two
        // concerns and makes their interaction correct:
        //
        //   * lock (CS1996) / LINQ non-source query clause (CS1995) / catch filter
        //     (CS7094) make awaiting illegal — but ONLY when they sit between the
        //     node and its nearest enclosing async function. An async lambda /
        //     local function is its OWN await context, so an OUTER lock / query /
        //     catch-filter does not reach across it. Because these cases share this
        //     walk with the async-boundary cases below, hitting an async lambda
        //     FIRST correctly returns "rewrite" before an outer lock is ever seen
        //     (pre-fix the unconditional pre-passes over-skipped that legal code and
        //     injected a factually wrong "cannot await inside a lock" comment).
        //
        //   * The async WHITELIST: rewrite ONLY when we positively reach an `async`
        //     method / local function / lambda, or a top-level statement. Reaching a
        //     type body or the compilation-unit root first means the `.Result` sits
        //     in a context that can never be `async` (property / field / operator /
        //     conversion / primary-ctor base / …), so we SKIP — closing the entire
        //     await-into-non-async corruption class in one place rather than
        //     enumerating every non-async member kind.
        //
        //   * Parameter defaults and attribute arguments are await-illegal even
        //     inside an async function (they require a compile-time constant), so
        //     they SKIP before the walk reaches the async boundary.
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                // Await-illegal sub-contexts — only reached while still inside the
                // nearest enclosing async scope (see note above).
                case LockStatementSyntax:
                    reason = "// MAF-AP-CONC-002: cannot await inside a lock block — refactor to SemaphoreSlim.WaitAsync or restructure.";
                    return true;
                case QueryExpressionSyntax q when !IsInAwaitableQuerySource(node, q):
                    reason = "// MAF-AP-CONC-002: cannot await inside this query clause — materialize the awaited value before the query.";
                    return true;
                case CatchFilterClauseSyntax:
                    reason = "// MAF-AP-CONC-002: cannot await inside a catch filter (`when (...)`) — hoist the awaited value into a variable before the try/catch.";
                    return true;
                case ParameterSyntax:
                case AttributeSyntax:
                    reason = "// MAF-AP-CONC-002: cannot await in a parameter default / attribute argument — these require a compile-time constant.";
                    return true;

                // Async-scope boundaries — these END the bounded walk.
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
    /// <summary>True only when the node sits in an await-legal source of THIS query:
    /// its OWN initial <c>from</c> source, or a <c>join … in</c> source it directly
    /// owns. Clauses inside a NESTED query (e.g. an inner query embedded in this
    /// query's await-illegal <c>let</c>/<c>where</c>/<c>select</c>) belong to that
    /// inner query, not this one — so we exclude them. Without the ownership check,
    /// <c>DescendantNodes()</c> reached an inner-query join source and wrongly marked
    /// an await-illegal outer-query position as legal, injecting an <c>await</c> that
    /// the compiler rejects with CS1995.</summary>
    private static bool IsInAwaitableQuerySource(SyntaxNode node, QueryExpressionSyntax query)
    {
        if (query.FromClause.Expression.Span.Contains(node.Span)
            && node.FirstAncestorOrSelf<QueryExpressionSyntax>() == query)
            return true;
        foreach (var join in query.DescendantNodes().OfType<JoinClauseSyntax>())
            if (join.FirstAncestorOrSelf<QueryExpressionSyntax>() == query
                && join.InExpression.Span.Contains(node.Span))
                return true;
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
                .Add(SyntaxFactory.EndOfLine(DetectNewLine(node))));
    }

    /// <summary>
    /// WM-42: the skip-with-TODO comment must use the FILE's newline, not a
    /// hard-coded LF — otherwise a CRLF file gets a lone LF spliced in, producing
    /// mixed line endings. Detect the convention from the first end-of-line trivia
    /// in the tree; default to LF for a single-line source with none.
    /// </summary>
    private static string DetectNewLine(SyntaxNode node)
    {
        var root = node.SyntaxTree?.GetRoot();
        if (root is not null)
            foreach (var trivia in root.DescendantTrivia())
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    return trivia.ToString();
        return "\n";
    }
}
