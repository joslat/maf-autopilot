using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Reflection;

namespace MafAutopilot.Resources;

/// <summary>
/// MCP Resources — expose all MAF knowledge files at stable maf:// URIs.
///
/// Static resources (no parameters):
///   maf://constraints  — hard constraints, always read before any migration task
///   maf://registry     — CS0618 obsolete API registry (YAML)
///   maf://guide        — full MAF 1.3.0 migration guide
///
/// Template resource (one per skill):
///   maf://skills?name=&lt;skillName&gt;  — any of the 11 skill SKILL.md files
/// </summary>
[McpServerResourceType]
public static class MafResources
{
    private static readonly Assembly _asm = typeof(MafResources).Assembly;

    // ------------------------------------------------------------------ //
    //  Static resources                                                    //
    // ------------------------------------------------------------------ //

    [McpServerResource(UriTemplate = "maf://constraints",
        MimeType = "text/markdown",
        Name = "maf-constraints",
        Title = "MAF 1.3.0 Hard Constraints")]
    [Description("The hard constraints that must never be violated during a MAF 1.3.0 migration. Always read this before starting any migration task.")]
    public static string GetConstraints() => ReadEmbedded("constraints.md");

    [McpServerResource(UriTemplate = "maf://registry",
        MimeType = "text/yaml",
        Name = "maf-registry",
        Title = "MAF Obsolete API Registry")]
    [Description("Machine-readable registry of known CS0618 obsolete API patterns with deterministic fix patterns.")]
    public static string GetRegistry() => ReadEmbedded("registry.yaml");

    [McpServerResource(UriTemplate = "maf://guide",
        MimeType = "text/markdown",
        Name = "maf-guide",
        Title = "MAF 1.3.0 Migration Guide")]
    [Description("Full MAF 1.3.0 migration reference guide with API signatures, before/after code patterns, and section-by-section explanations.")]
    public static string GetGuide() => ReadEmbedded("guide.md");

    // ------------------------------------------------------------------ //
    //  Template resource: individual skill documents                       //
    // ------------------------------------------------------------------ //

    [McpServerResource(UriTemplate = "maf://skills{?name}",
        MimeType = "text/markdown",
        Name = "maf-skill",
        Title = "MAF Skill Document")]
    [Description(
        "A specific MAF skill document. Pass name= one of: " +
        "cs0618-hunter, dotnet-inspect, fan-in-static-analyzer, fan-out-validator, " +
        "maf-migration-guide, maf-release-watcher, migration-plan-creator, " +
        "migration-retrospective, nuget-diff-analyzer, obsolete-api-registry, workflow-smoke-tester")]
    public static string GetSkill(string name)
    {
        if (!AllowedSkillNames.Contains(name))
            throw new ArgumentException(
                $"Unknown skill '{name}'. Available: {string.Join(", ", AllowedSkillNames)}");

        return ReadEmbedded($"skills/{name}.md");
    }

    // ------------------------------------------------------------------ //
    //  Internal helpers                                                    //
    // ------------------------------------------------------------------ //

    private static readonly IReadOnlySet<string> AllowedSkillNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cs0618-hunter",
            "dotnet-inspect",
            "fan-in-static-analyzer",
            "fan-out-validator",
            "maf-migration-guide",
            "maf-release-watcher",
            "migration-plan-creator",
            "migration-retrospective",
            "nuget-diff-analyzer",
            "obsolete-api-registry",
            "workflow-smoke-tester",
        };

    private static string ReadEmbedded(string logicalName)
    {
        using var stream = _asm.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{logicalName}' not found. " +
                $"Available: {string.Join(", ", _asm.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
