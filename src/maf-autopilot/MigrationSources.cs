namespace MafDoctor;

/// <summary>
/// Single source of truth for which "migrate FROM" source frameworks the toolkit
/// supports today. Consumed by the CLI (<c>migrate-scan</c>), the MCP resource
/// (<c>maf://migrate-from</c>), and the <c>maf-migrate-from</c> prompt so the
/// allow-list (and its aliases) cannot drift across those three surfaces.
///
/// Adding a new source framework (e.g. AutoGen) is a one-line edit here.
/// </summary>
internal static class MigrationSources
{
    /// <summary>The canonical Semantic Kernel source id (used in URIs / reports).</summary>
    public const string SemanticKernel = "semantic-kernel";

    /// <summary>Human-readable list of supported sources + aliases, for error / UX text.</summary>
    public const string SupportedDescription = "semantic-kernel (alias: sk)";

    /// <summary>
    /// Canonicalizes a user-supplied source to its canonical id, or <c>null</c> if the
    /// source is not supported. Trims, lower-cases, and resolves aliases (e.g. <c>sk</c>).
    /// </summary>
    public static string? Canonicalize(string? source)
        => (source ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "semantic-kernel" or "sk" => SemanticKernel,
            _ => null,
        };

    /// <summary>True when <paramref name="source"/> is a supported source framework (alias-aware).</summary>
    public static bool IsSupported(string? source) => Canonicalize(source) is not null;
}
