using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafAutopilot.Tools.Rewriters;

/// <summary>
/// Rule: <c>MAF-AP-SEC-003</c> / <c>MAF003</c>.
///
/// Rewrite: any object-initializer assignment <c>EnableSensitiveData = true</c>
/// is dropped from the initializer list (the property defaults to false, so
/// removing the assignment yields the safe default).
///
/// Matches BOTH bare initializer assignments AND dictionary-style
/// <c>AdditionalProperties</c> entries like <c>["EnableSensitiveData"] = true</c>.
/// </summary>
internal sealed class EnableSensitiveDataRewriter : CSharpSyntaxRewriter, IRuleRewriter
{
    public string RuleId => "MAF-AP-SEC-003";

    public override SyntaxNode? VisitInitializerExpression(InitializerExpressionSyntax node)
    {
        var filtered = node.Expressions
            .Where(e => !IsEnableSensitiveDataTrueAssignment(e))
            .ToArray();

        if (filtered.Length == node.Expressions.Count)
            return base.VisitInitializerExpression(node); // no removal

        // Rebuild the initializer with the filtered list, preserving the
        // outer braces and any trivia on the unchanged expressions.
        var newList = SyntaxFactory.SeparatedList(filtered);
        return node.WithExpressions(newList);
    }

    private static bool IsEnableSensitiveDataTrueAssignment(ExpressionSyntax expr)
    {
        if (expr is not AssignmentExpressionSyntax assign)
            return false;

        // Pattern A: `EnableSensitiveData = true`
        if (assign.Left is IdentifierNameSyntax id
            && id.Identifier.ValueText == "EnableSensitiveData"
            && assign.Right is LiteralExpressionSyntax lit
            && lit.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return true;
        }

        // Pattern B: `["EnableSensitiveData"] = true` (collection-initializer).
        if (assign.Left is ImplicitElementAccessSyntax indexer
            && indexer.ArgumentList.Arguments.Count == 1
            && indexer.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax keyLit
            && keyLit.IsKind(SyntaxKind.StringLiteralExpression)
            && keyLit.Token.ValueText == "EnableSensitiveData"
            && assign.Right is LiteralExpressionSyntax valueLit
            && valueLit.IsKind(SyntaxKind.TrueLiteralExpression))
        {
            return true;
        }

        return false;
    }
}
