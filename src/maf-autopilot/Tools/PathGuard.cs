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

    /// <summary>
    /// Validates that a secondary path parameter (a file or directory the LLM
    /// names alongside the primary <c>repoPath</c>) stays inside that root,
    /// regardless of whether the caller supplied an absolute or relative form.
    ///
    /// Reject conditions:
    ///   - <paramref name="candidatePath"/> contains a NUL / CR / LF / shell
    ///     metacharacter, or a <c>..</c> path segment (defense in depth — the
    ///     containment check below would catch path traversal anyway, but the
    ///     explicit segment rejection produces a clearer error message).
    ///   - the canonicalized candidate does not lie under the canonicalized
    ///     repository root (catches absolute paths to anywhere on disk and
    ///     any leftover <c>..</c>-driven escapes from non-segment-style input).
    ///   - any segment of the resolved chain from candidate up to (but not
    ///     including) the root is a reparse point (symlink / junction). The
    ///     repository root itself may legitimately be reached through a
    ///     symlink the user set up; we only reject reparse points encountered
    ///     <em>inside</em> the root.
    ///
    /// On success returns the canonical absolute path of the candidate so the
    /// caller can use it directly. On failure throws <see cref="ArgumentException"/>
    /// with a non-leaky message (no echo of the input path — error reporting
    /// telemetry, if added later, should not leak filesystem layout).
    ///
    /// Anchored to:
    ///   - OWASP MCP Top 10 — MCP04 Command Injection / path-escape variant.
    ///   - v1.1 security hardening plan §3.1 row C1 (AutoFix specificFile).
    /// </summary>
    public static string ValidateContainment(string repoPath, string candidatePath, string parameterName = "path")
    {
        if (string.IsNullOrWhiteSpace(repoPath))
            throw new ArgumentException("repoPath must not be empty.", nameof(repoPath));
        if (string.IsNullOrWhiteSpace(candidatePath))
            throw new ArgumentException($"{parameterName} must not be empty.", nameof(candidatePath));

        // Defense-in-depth char + segment rejection before any FS resolution.
        if (candidatePath.IndexOfAny(['\0', '\r', '\n', '"', ';', '|', '&']) >= 0)
            throw new ArgumentException($"{parameterName} contains invalid characters.", nameof(candidatePath));
        var normalized = candidatePath.Replace('\\', '/');
        foreach (var segment in normalized.Split('/'))
            if (segment == "..")
                throw new ArgumentException(
                    $"{parameterName} must not contain '..' segments (path traversal).",
                    nameof(candidatePath));

        // Canonicalize both sides. GetFullPath collapses any remaining
        // syntactic redundancy and produces an OS-native absolute path.
        var rootFull = Path.GetFullPath(repoPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var resolvedFull = Path.GetFullPath(
            Path.IsPathRooted(candidatePath)
                ? candidatePath
                : Path.Combine(repoPath, candidatePath));

        var rootBoundary = rootFull + Path.DirectorySeparatorChar;
        if (!resolvedFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase)
            && !resolvedFull.StartsWith(rootBoundary, StringComparison.OrdinalIgnoreCase))
        {
            // Do not echo the input — error-reporting telemetry should not
            // leak filesystem layout. The parameter name + class of failure
            // is enough for an honest caller to diagnose.
            throw new ArgumentException(
                $"{parameterName} resolves outside the repository root.",
                nameof(candidatePath));
        }

        // Walk the resolved path from candidate up to (but not including) the
        // root. A reparse point anywhere in that chain would let a hostile
        // workspace redirect reads/writes outside the root that we just
        // confirmed contains the resolution syntactically.
        //
        // Two distinct probes are required:
        //   (a) the candidate file itself, if it exists — Path.GetFullPath
        //       does NOT resolve symlinks (it only canonicalizes `.`/`..`
        //       syntactically), so a file-level symlink at <root>/safe.cs ->
        //       /etc/passwd would pass the StartsWith check.
        //   (b) every parent directory up to the root — covers junctions
        //       and directory-level symlinks placed inside the workspace.
        //
        // probe.Exists short-circuits non-existent intermediates so we do not
        // need to read Attributes (which returns (FileAttributes)(-1) on a
        // missing path).
        if (File.Exists(resolvedFull) || Directory.Exists(resolvedFull))
        {
            var leafAttrs = File.GetAttributes(resolvedFull);
            if ((leafAttrs & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException(
                    $"{parameterName} is a symlink/reparse point.",
                    nameof(candidatePath));
            }
        }

        var startPath = File.Exists(resolvedFull)
            ? Path.GetDirectoryName(resolvedFull) ?? rootFull
            : resolvedFull;
        var probe = new DirectoryInfo(startPath);

        while (probe is not null
            && !probe.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Equals(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            if (probe.Exists && (probe.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException(
                    $"{parameterName} traverses a symlink/reparse point inside the repository.",
                    nameof(candidatePath));
            }
            probe = probe.Parent;
        }

        return resolvedFull;
    }
}
