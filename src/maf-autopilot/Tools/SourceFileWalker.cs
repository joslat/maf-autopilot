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
    /// Enumerates every <c>*.cs</c> file under <paramref name="repoRoot"/>,
    /// excluding common build artefact paths (<c>/bin/</c>, <c>/obj/</c>).
    /// Symlinks are followed by <see cref="Directory.EnumerateFiles(string, string, SearchOption)"/>
    /// — acceptable because the caller has already validated the input path
    /// via <see cref="PathGuard.ValidateRepoPath"/>.
    /// </summary>
    public static IEnumerable<string> EnumerateCsFiles(string repoRoot)
    {
        foreach (var path in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
        {
            var n = path.Replace('\\', '/');
            if (n.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (n.Contains("/obj/", StringComparison.Ordinal)) continue;
            yield return path;
        }
    }

    /// <summary>
    /// Returns the path of <paramref name="file"/> relative to <paramref name="root"/>,
    /// with forward-slash separators. Falls back to the full path if no relation can
    /// be established (defensive — never throws).
    /// </summary>
    public static string MakeRelative(string root, string file)
    {
        var rootFull = Path.GetFullPath(root);
        var fileFull = Path.GetFullPath(file);
        if (fileFull.StartsWith(rootFull, PathComparison))
            return fileFull.Substring(rootFull.Length).TrimStart('/', '\\').Replace('\\', '/');
        return fileFull.Replace('\\', '/');
    }
}
