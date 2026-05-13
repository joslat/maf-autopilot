using Microsoft.CodeAnalysis;

namespace MafAutopilot.Tools.Rewriters;

/// <summary>
/// Contract for a deterministic per-rule auto-fixer. Implementations are
/// `Microsoft.CodeAnalysis.CSharp.CSharpSyntaxRewriter` subclasses that mutate
/// a syntax tree to remove a single registry rule's anti-pattern.
///
/// **Determinism contract.** A rewriter MUST be idempotent — running it
/// twice on the same input produces the same output. It MUST preserve trivia
/// (comments, whitespace) on every node it touches. It MUST never alter
/// syntax it doesn't recognise.
///
/// See <see cref="AutoFixTool"/> for the dispatch + filesystem write-back.
/// </summary>
internal interface IRuleRewriter
{
    /// <summary>The registry rule ID this rewriter remediates.</summary>
    string RuleId { get; }

    /// <summary>
    /// Apply the rewrite. Returns the new root (same instance if no changes).
    /// </summary>
    SyntaxNode Visit(SyntaxNode root);
}
