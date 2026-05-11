using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MafAutopilot.Analyzers;

/// <summary>
/// MAF003 — EnableSensitiveData = true outside of test code.
///
/// Sensitive-data logging in production leaks PII, prompts, and tool arguments
/// to whatever log sink is wired. Allowed in tests/samples; not in production.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SensitiveDataAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MAF003";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "EnableSensitiveData = true outside test code",
        messageFormat: "EnableSensitiveData = true leaks prompts, tool arguments, and message contents into log sinks. Gate behind `#if DEBUG` or `builder.Environment.IsDevelopment()` before shipping.",
        category: "MAF.Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Sensitive-data logging is intended for development diagnostics only. Production telemetry should default to redacted attributes.",
        // Anchor fragments are slugified by GitHub from the full heading text. Link
        // at the document root; the rule ID is grep-able inside the page.
        helpLinkUri: "https://github.com/joslat/maf-autopilot/blob/main/.github/skills/maf-anti-pattern-scanner/SKILL.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var node = (AssignmentExpressionSyntax)context.Node;

        // Looking for: <something>.EnableSensitiveData = true
        // or:          EnableSensitiveData = true  (object initializer)
        var leftText = node.Left.ToString();
        var simpleName = leftText.Contains(".")
            ? leftText.Substring(leftText.LastIndexOf('.') + 1)
            : leftText;
        if (simpleName != "EnableSensitiveData") return;

        if (node.Right is not LiteralExpressionSyntax literal) return;
        if (literal.Kind() != SyntaxKind.TrueLiteralExpression) return;

        var filePath = context.Node.SyntaxTree.FilePath ?? string.Empty;
        if (IsTestFile(filePath)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
    }

    private static bool IsTestFile(string filePath) => TestFileHeuristic.IsTestFile(filePath);
}
