using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MafAutopilot.Analyzers;

/// <summary>
/// MAF001 — fan-out handler must return Task&lt;T&gt; or ValueTask&lt;T&gt;.
///
/// Detects the silent fan-in starvation pattern at WRITE-TIME, before the
/// developer ever commits. A [MessageHandler] method that returns void,
/// Task, or ValueTask (non-generic) produces NO output message — the fan-in
/// barrier then starves silently. This is not a build error; without an
/// analyzer, only static scanning (post-edit) or runtime tracing catches it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FanOutHandlerAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MAF001";

    private static readonly LocalizableString Title =
        "Fan-out handler must return Task<T> or ValueTask<T>";

    private static readonly LocalizableString MessageFormat =
        "Method '{0}' is decorated with [MessageHandler] but returns '{1}' — this produces no downstream message and silently starves the fan-in barrier. Return Task<T> or ValueTask<T> where T is the downstream message type.";

    private static readonly LocalizableString Description =
        "A [MessageHandler] method that returns void, Task, or ValueTask (non-generic) produces no output. The workflow exits cleanly but incompletely — invisible to dotnet build, visible only at runtime. Return a typed Task<TMessage> / ValueTask<TMessage>.";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: "MAF.Workflow",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: "https://github.com/joslat/maf-autopilot/blob/main/.github/skills/maf-fan-out-validator/SKILL.md");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        if (!HasMessageHandlerAttribute(method)) return;

        var returnType = method.ReturnType.ToString().Trim();
        if (IsFanOutSafeReturnType(returnType)) return;

        var diagnostic = Diagnostic.Create(
            Rule,
            method.ReturnType.GetLocation(),
            method.Identifier.ValueText,
            returnType);
        context.ReportDiagnostic(diagnostic);
    }

    private static bool HasMessageHandlerAttribute(MethodDeclarationSyntax method) =>
        method.AttributeLists
            .SelectMany(list => list.Attributes)
            .Any(attr =>
            {
                var name = attr.Name.ToString();
                var simpleName = name.Contains(".") ? name.Substring(name.LastIndexOf('.') + 1) : name;
                return simpleName == "MessageHandler" || simpleName == "MessageHandlerAttribute";
            });

    private static bool IsFanOutSafeReturnType(string returnType)
    {
        var simple = returnType.Contains(".")
            ? returnType.Substring(returnType.LastIndexOf('.') + 1)
            : returnType;

        // Generic Task<T> / ValueTask<T> / IAsyncEnumerable<T> are all fan-out-safe.
        return simple.StartsWith("Task<", System.StringComparison.Ordinal)
            || simple.StartsWith("ValueTask<", System.StringComparison.Ordinal)
            || simple.StartsWith("IAsyncEnumerable<", System.StringComparison.Ordinal);
    }
}
