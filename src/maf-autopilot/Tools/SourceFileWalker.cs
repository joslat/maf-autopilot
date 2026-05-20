using System.Runtime.InteropServices;

namespace MafAutopilot.Tools;

/// <summary>
/// Shared file-enumeration + relative-path helpers used by every scanner that
/// walks a repo for <c>*.cs</c> files. Extracted 2026-05-12 — was previously
/// duplicated verbatim in 6+ tools, and the slow-drift risk was real (a
/// `/bin/` exclusion fix would land in some tools but not others).
///
/// See Phase E.5 in `docs/maf-migration-toolkit-plan.md`.
/// </summary>
internal static class SourceFileWalker
{
    // Path comparison is case-insensitive on Windows (paths /Foo and /foo
    // are the same file) and case-sensitive on POSIX (they are NOT the same).
    // Using OrdinalIgnoreCase unconditionally broke MakeRelative on Linux/macOS
    // when the supplied root differed in case from the on-disk path — the
    // function would silently fall back to the full path instead of producing
    // the intended repo-relative result.
    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Shared <see cref="EnumerationOptions"/> for recursive .cs walks.
    ///
    /// <para><c>IgnoreInaccessible = true</c> is critical when scanning paths
    /// under <c>/tmp/</c> on Linux CI (and any user-repo containing protected
    /// subdirs) — without it, an unreadable subdirectory anywhere in the
    /// tree throws <see cref="UnauthorizedAccessException"/> and aborts the
    /// whole scan. Bug surfaced in the v1.0.0 release pipeline
    /// (<c>/tmp/systemd-private-*</c> is owned by root on the GitHub Actions
    /// Ubuntu runner).</para>
    ///
    /// <para>Phase 5.3 — <c>AttributesToSkip = ReparsePoint | Hidden | System</c>:</para>
    /// <list type="bullet">
    ///   <item><c>ReparsePoint</c> skips symlinks / junctions. The default
    ///         <see cref="EnumerationOptions"/> follow them, which combined
    ///         with a path-escape in a tool parameter would let a hostile
    ///         workspace redirect a recursive scan outside the repo root.
    ///         <see cref="PathGuard.ValidateContainment"/> guards the entry
    ///         point; this guards the walker.</item>
    ///   <item><c>Hidden</c> + <c>System</c> skip files the user wouldn't
    ///         expect us to read (<c>.git/</c>, <c>node_modules/</c> are
    ///         already filtered by the manual <c>/bin/</c>/<c>/obj/</c>
    ///         post-walk filter; this adds the OS-level marker too).</item>
    /// </list>
    /// </summary>
    private static readonly EnumerationOptions RecursiveCsOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
                         | FileAttributes.Hidden
                         | FileAttributes.System,
    };

    /// <summary>
    /// Enumerates every <c>*.cs</c> file under <paramref name="repoRoot"/>,
    /// excluding common build artefact paths (<c>/bin/</c>, <c>/obj/</c>).
    /// Unreadable subdirectories are silently skipped (see
    /// <see cref="RecursiveCsOptions"/>). Symlinks / reparse points are NOT
    /// followed (Phase 5.3 hardening). Caller has already validated the input
    /// path via <see cref="PathGuard.ValidateRepoPath"/>.
    /// </summary>
    public static IEnumerable<string> EnumerateCsFiles(string repoRoot)
    {
        foreach (var path in Directory.EnumerateFiles(repoRoot, "*.cs", RecursiveCsOptions))
        {
            var n = path.Replace('\\', '/');
            if (n.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (n.Contains("/obj/", StringComparison.Ordinal)) continue;
            yield return path;
        }
    }

    /// <summary>
    /// Returns the path of <paramref name="file"/> relative to
    /// <paramref name="root"/>, with forward-slash separators.
    ///
    /// <para>Phase 5.4 — throws <see cref="InvalidOperationException"/> when
    /// the file is not under the root. Previously this returned the full
    /// absolute path on mismatch, leaking host filesystem layout into JSON
    /// output (a soft information-disclosure lane via tool output that an
    /// agent then renders back to the user / its own log). Containment is
    /// the upstream caller's responsibility — by the time we're here, an
    /// out-of-root file is an invariant violation worth surfacing loudly.</para>
    /// </summary>
    public static string MakeRelative(string root, string file)
    {
        var rootFull = Path.GetFullPath(root);
        var fileFull = Path.GetFullPath(file);
        if (fileFull.StartsWith(rootFull, PathComparison))
            return fileFull.Substring(rootFull.Length).TrimStart('/', '\\').Replace('\\', '/');

        // Non-leaky message: name the parameter class, not the input.
        throw new InvalidOperationException(
            "MakeRelative: file is outside the repository root. " +
            "Caller must validate containment (PathGuard.ValidateContainment) before invoking.");
    }
}
