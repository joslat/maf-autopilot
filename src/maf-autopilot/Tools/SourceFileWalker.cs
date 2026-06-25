using System.Runtime.InteropServices;

namespace MafDoctor.Tools;

/// <summary>
/// Shared file-enumeration + relative-path helpers used by every scanner that
/// walks a repo for <c>*.cs</c> files. Extracted 2026-05-12 — was previously
/// duplicated verbatim in 6+ tools, and the slow-drift risk was real (a
/// `/bin/` exclusion fix would land in some tools but not others). (Phase E.5.)
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
        => EnumerateCsFiles(repoRoot, excludes: null);

    /// <summary>
    /// Overload that additionally skips any file whose <em>repo-relative</em>
    /// path contains one of <paramref name="excludes"/>. Used by callers that
    /// want to scan product code only — e.g. the drift detector excluding
    /// <c>samples/</c> and test fixtures so the health grade isn't dominated by
    /// the repo's intentional anti-pattern bait. Matching is plain substring
    /// (not glob) by design: simple, no edge cases, and the <em>relative</em>
    /// path is tested (not the absolute one) so the repo's on-disk location
    /// can't accidentally match an exclude token.
    /// </summary>
    public static IEnumerable<string> EnumerateCsFiles(string repoRoot, IReadOnlyList<string>? excludes)
    {
        var skip = (excludes ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Replace('\\', '/'))
            .ToArray();

        foreach (var path in Directory.EnumerateFiles(repoRoot, "*.cs", RecursiveCsOptions))
        {
            var n = path.Replace('\\', '/');
            if (n.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (n.Contains("/obj/", StringComparison.Ordinal)) continue;
            if (skip.Length > 0)
            {
                var rel = MakeRelative(repoRoot, path);
                if (skip.Any(s => rel.Contains(s, PathComparison))) continue;
            }
            yield return path;
        }
    }

    /// <summary>
    /// Enumerates every <c>*.csproj</c> file under <paramref name="repoRoot"/>.
    /// Same EnumerationOptions as <see cref="EnumerateCsFiles"/> — no symlinks,
    /// no hidden/system, inaccessible subdirs skipped. Phase 5.G fixup: added
    /// so <c>DraftIssueTool</c> and <c>FanOutValidatorTool</c> can migrate off
    /// the raw <c>Directory.EnumerateFiles</c> / <c>GetFiles</c> calls that
    /// followed symlinks by default.
    /// </summary>
    public static IEnumerable<string> EnumerateCsprojFiles(string repoRoot)
    {
        return Directory.EnumerateFiles(repoRoot, "*.csproj", RecursiveCsOptions);
    }

    /// <summary>
    /// Enumerates every file under <paramref name="repoRoot"/> matching
    /// <paramref name="searchPattern"/>, using the SAME hardened options as the
    /// <c>.cs</c> walk (no symlinks / hidden / system, inaccessible subdirs skipped)
    /// and excluding <c>/bin/</c> + <c>/obj/</c> artefact paths. For non-<c>.cs</c>
    /// project metadata such as <c>Directory.Packages.props</c>, so those walks don't
    /// reintroduce the raw <c>Directory.EnumerateFiles</c> symlink-following risk.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string repoRoot, string searchPattern)
    {
        foreach (var path in Directory.EnumerateFiles(repoRoot, searchPattern, RecursiveCsOptions))
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
