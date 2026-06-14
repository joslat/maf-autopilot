using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MafAutopilot.Analyzers;

/// <summary>
/// MAF002 — DefaultAzureCredential in production.
///
/// Flags <c>new DefaultAzureCredential()</c> usage outside test code. The
/// credential walks an authentication chain whose order can change between
/// SDK versions; production deployments should NEVER tolerate that surprise.
/// Pin to <c>ManagedIdentityCredential</c>, <c>WorkloadIdentityCredential</c>,
/// or an explicit secret credential.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DefaultAzureCredentialAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MAF002";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid DefaultAzureCredential in production code",
        messageFormat: "Replace 'new DefaultAzureCredential()' with an explicit credential type (ManagedIdentityCredential for Azure-hosted, WorkloadIdentityCredential for Kubernetes, or a specific secret credential). Chain ordering is implementation-defined and can change between SDK versions.",
        category: "MAF.Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DefaultAzureCredential walks a chain that may include developer credentials, env vars, and managed identity. Production code should pin a specific credential type.",
        // Anchor fragments are slugified by GitHub from the full heading text, which
        // is fragile across heading edits. Link at the document root and rely on
        // the rule ID being grep-able inside the page.
        helpLinkUri: "https://github.com/joslat/maf-doctor/blob/main/.github/skills/maf-anti-pattern-scanner/SKILL.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        var node = (ObjectCreationExpressionSyntax)context.Node;
        var typeName = node.Type.ToString();
        var simpleName = typeName.Contains(".")
            ? typeName.Substring(typeName.LastIndexOf('.') + 1)
            : typeName;
        if (simpleName != "DefaultAzureCredential") return;

        // Skip test projects/files — they're allowed to use DefaultAzureCredential.
        var filePath = context.Node.SyntaxTree.FilePath ?? string.Empty;
        if (IsTestFile(filePath)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
    }

    private static bool IsTestFile(string filePath)
        => TestFileHeuristic.IsTestFile(filePath);
}

internal static class TestFileHeuristic
{
    /// <summary>
    /// Heuristic: a file is "test infrastructure" if any of these hold:
    ///   - any path segment is `tests` / `test` / `samples` / `unittests` / `integrationtests` / `e2e`
    ///   - filename ends with `tests.cs` / `test.cs` / `fixtures.cs`
    ///   - directory name (any segment) ends with `.tests` / `.unittests` / `.integrationtests`
    /// Generous on purpose — false negatives on this check turn into noisy warnings
    /// on production code, which is the worse outcome for analyzer usability.
    /// </summary>
    public static bool IsTestFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        var normalized = filePath.Replace('\\', '/').ToLowerInvariant();

        foreach (var segment in normalized.Split('/'))
        {
            if (segment is "tests" or "test" or "samples" or "unittests" or "integrationtests" or "e2e")
                return true;
            if (segment.EndsWith(".tests", System.StringComparison.Ordinal)
                || segment.EndsWith(".unittests", System.StringComparison.Ordinal)
                || segment.EndsWith(".integrationtests", System.StringComparison.Ordinal)
                || segment.EndsWith(".e2e", System.StringComparison.Ordinal))
                return true;
        }

        return normalized.EndsWith("tests.cs", System.StringComparison.Ordinal)
            || normalized.EndsWith("test.cs", System.StringComparison.Ordinal)
            || normalized.EndsWith("fixtures.cs", System.StringComparison.Ordinal);
    }
}
