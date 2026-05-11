namespace MafAutopilot.Tools;

/// <summary>
/// Shared input-validation for path parameters that come from the LLM.
/// Every tool that accepts a `repoPath` / `projectPath` must run input through
/// <see cref="ValidateRepoPath"/> before any I/O. Blocks: empty/whitespace,
/// control characters / shell metacharacters, segment-level <c>..</c>
/// (path traversal), and non-existent directories.
///
/// Extracted 2026-05-12 — was previously duplicated (or absent) across
/// 12+ tool classes. See Phase E.1 in `docs/maf-migration-toolkit-plan.md`.
/// </summary>
internal static class PathGuard
{
    /// <summary>
    /// Returns an error message if the path is unsafe to use, or null when it's OK.
    /// </summary>
    public static string? ValidateRepoPath(string? path, string parameterName = "repoPath")
    {
        if (string.IsNullOrWhiteSpace(path))
            return $"Error: {parameterName} must not be empty.";

        if (path.IndexOfAny(['\0', '\r', '\n', '"', ';', '|', '&']) >= 0)
            return $"Error: {parameterName} contains invalid characters.";

        // Block `..` as a path segment. Normalize directory separators first so
        // the check works on Windows ('\\') and POSIX ('/') alike.
        var normalized = path.Replace('\\', '/');
        foreach (var segment in normalized.Split('/'))
            if (segment == "..")
                return $"Error: {parameterName} must not contain '..' segments (path traversal).";

        if (!Directory.Exists(path))
            return $"Error: directory does not exist: '{path}'.";

        return null;
    }
}
