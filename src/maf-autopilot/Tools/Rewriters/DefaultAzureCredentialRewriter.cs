using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MafAutopilot.Tools.Rewriters;

/// <summary>
/// Rule: <c>MAF-AP-SEC-001</c> / <c>MAF002</c>.
///
/// Rewrite: <c>new DefaultAzureCredential(...)</c> → <c>new ManagedIdentityCredential(...)</c>.
///
/// The rewriter is symbol-name-aware only (no semantic model). If the
/// consumer aliased the type via <c>using DefaultAzureCredential =
/// Some.Other.Type;</c>, the rewriter will still flip it (limitation
/// documented in the registry entry's notes).
/// </summary>
internal sealed class DefaultAzureCredentialRewriter : CSharpSyntaxRewriter, IRuleRewriter
{
    public string RuleId => "MAF-AP-SEC-001";

    public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        if (node.Type is IdentifierNameSyntax id && id.Identifier.ValueText == "DefaultAzureCredential")
        {
            // Preserve all trivia on the type identifier (leading/trailing
            // whitespace, comments). The replacement borrows the same trivia.
            var replacementType = SyntaxFactory.IdentifierName("ManagedIdentityCredential")
                .WithLeadingTrivia(id.GetLeadingTrivia())
                .WithTrailingTrivia(id.GetTrailingTrivia());
            return node.WithType(replacementType);
        }
        return base.VisitObjectCreationExpression(node);
    }
}
