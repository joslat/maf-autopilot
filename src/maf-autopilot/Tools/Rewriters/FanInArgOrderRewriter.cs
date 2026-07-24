using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafDoctor.Tools.Rewriters;

/// <summary>
/// Rule: <c>MAF130-FAN-IN-001</c>.
///
/// Rewrite: <c>builder.AddFanInBarrierEdge(target, sources)</c> →
/// <c>builder.AddFanInBarrierEdge(sources, target)</c>.
///
/// Heuristic for "which arg is which": the SOURCES argument is a
/// collection-literal (array creation, implicit array, or argument labelled
/// <c>sources:</c>). The TARGET argument is a single identifier or member-
/// access expression labelled <c>target:</c>. If the call is ALREADY in the
/// new (sources, target) order, the rewriter is a no-op.
/// </summary>
internal sealed class FanInArgOrderRewriter : CSharpSyntaxRewriter, IRuleRewriter
{
    public string RuleId => "MAF130-FAN-IN-001";

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (!IsAddFanInBarrierEdge(node))
            return base.VisitInvocationExpression(node);

        var args = node.ArgumentList.Arguments;
        if (args.Count != 2)
            return base.VisitInvocationExpression(node);

        if (!LooksLikeWrongOrder(args[0], args[1]))
            return base.VisitInvocationExpression(node);

        // Swap the two argument VALUES and strip the named-argument labels (they no
        // longer match the new overload's parameter positions), preserving trivia per
        // the IRuleRewriter contract. The old `WithTriviaFrom(peer)` copied the OTHER
        // slot's trivia onto each moved value, which dropped a comment carried by the
        // separator and misattributed each value's own trailing comment. Instead:
        //   * keep each slot's LEADING trivia (indentation stays at its position),
        //   * carry each value's OWN TRAILING trivia (its trailing comment travels
        //     with the value), and
        //   * reuse the ORIGINAL separator token so an inter-argument comment that
        //     lived on the comma is not lost.
        var newFirst = StripNamedLabel(args[1])
            .WithLeadingTrivia(args[0].GetLeadingTrivia())
            .WithTrailingTrivia(args[1].GetTrailingTrivia());
        var newSecond = StripNamedLabel(args[0])
            .WithLeadingTrivia(args[1].GetLeadingTrivia())
            .WithTrailingTrivia(args[0].GetTrailingTrivia());

        var newArgs = SyntaxFactory.SeparatedList(
            new[] { newFirst, newSecond },
            new[] { args.GetSeparator(0) });
        return node.WithArgumentList(node.ArgumentList.WithArguments(newArgs));
    }

    private static bool IsAddFanInBarrierEdge(InvocationExpressionSyntax node) =>
        node.Expression is MemberAccessExpressionSyntax mae
        && mae.Name.Identifier.ValueText == "AddFanInBarrierEdge";

    /// <summary>
    /// Returns true if the first argument is a single-value expression and the
    /// second is a collection — i.e. the call is in the obsolete (target,
    /// sources) order. The "named label" check is the most reliable signal;
    /// otherwise we use syntactic shape.
    /// </summary>
    private static bool LooksLikeWrongOrder(ArgumentSyntax first, ArgumentSyntax second)
    {
        // Named labels are unambiguous.
        var firstLabel = first.NameColon?.Name.Identifier.ValueText;
        var secondLabel = second.NameColon?.Name.Identifier.ValueText;
        if (firstLabel == "target" && secondLabel == "sources") return true;
        if (firstLabel == "sources" && secondLabel == "target") return false;

        // Positional: heuristics on syntactic shape.
        return !LooksLikeCollection(first.Expression) && LooksLikeCollection(second.Expression);
    }

    private static bool LooksLikeCollection(ExpressionSyntax expr) => expr switch
    {
        ArrayCreationExpressionSyntax => true,            // new[] { ... } / new T[] { ... }
        ImplicitArrayCreationExpressionSyntax => true,    // new[] { ... }
        CollectionExpressionSyntax => true,               // [a, b, c] (C# 12)
        InitializerExpressionSyntax => true,              // { ... }
        ObjectCreationExpressionSyntax oce when oce.Type is ArrayTypeSyntax => true,
        // WM-42/WM-43: match KNOWN collection types by their rightmost simple name,
        // not a `Contains("List")` substring — the latter also matched non-collection
        // types like `ListenerNode` / `Playlist` / `Blacklist`, which could trigger a
        // WRONG argument swap on a correctly-ordered call.
        ObjectCreationExpressionSyntax oce when IsKnownCollectionType(oce.Type) => true,
        _ => false,
    };

    private static readonly HashSet<string> CollectionTypeNames = new(StringComparer.Ordinal)
    {
        "List", "IList", "IReadOnlyList", "IEnumerable", "ICollection",
        "IReadOnlyCollection", "Collection", "HashSet", "ISet",
    };

    private static bool IsKnownCollectionType(TypeSyntax type) =>
        RightmostName(type) is { } n && CollectionTypeNames.Contains(n);

    // `List<T>` → "List", `System.Collections.Generic.List<T>` → "List", `List` → "List".
    private static string? RightmostName(TypeSyntax type) => type switch
    {
        GenericNameSyntax g => g.Identifier.ValueText,
        IdentifierNameSyntax i => i.Identifier.ValueText,
        QualifiedNameSyntax q => RightmostName(q.Right),
        _ => null,
    };

    private static ArgumentSyntax StripNamedLabel(ArgumentSyntax arg) =>
        arg.NameColon is not null ? arg.WithNameColon(null) : arg;
}
