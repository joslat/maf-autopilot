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
    /// Hard ceiling on how many files a single repo walk yields. #148 finding 2 —
    /// none of the scanning tools capped scan size, so an accidentally-huge
    /// target (a big monorepo, a mounted drive, or a misresolved cwd) turned one
    /// call into an unbounded-time, unbounded-memory walk. Iterator methods
    /// can't use <c>out</c> parameters, so truncation is surfaced by mutating
    /// this shared, caller-owned counter as <see cref="EnumerateCsFiles(string, IReadOnlyList{string}?, ScanBudget)"/>
    /// yields — inspect <see cref="Truncated"/> after enumerating.
    ///
    /// The default (50,000 files) is deliberately generous: this repo alone has
    /// ~155 <c>.cs</c> files, and even a very large real-world C# codebase rarely
    /// exceeds a few tens of thousands — the cap exists to bound pathological
    /// targets, not to constrain normal repos.
    /// </summary>
    internal sealed class ScanBudget
    {
        public int MaxFiles { get; init; } = 50_000;

        // A single pathologically large file (deeply-nested generics, a giant
        // generated source file) fully materialized via File.ReadAllText can
        // dominate memory just like an unbounded file COUNT can dominate time.
        // Lives here (not per-tool) so every caller of EnumerateCsFiles gets the
        // same protection — matches the order of magnitude of the CI
        // "compiler-bomb cap" (maf-pr-audit.yml).
        public long MaxFileBytes { get; init; } = 10 * 1024 * 1024; // 10 MB

        // F-10 (2026-07-19 security assessment) — MaxFiles × MaxFileBytes bounds
        // any SINGLE file, but not the sum across an accepted batch: 50,000 files
        // just under the 10 MB per-file cap is a 500 GB theoretical aggregate.
        // This caps total accepted bytes across one enumeration, independent of
        // file count — a caller that retains every source body in memory at
        // once (e.g. SemanticKernelDetectorTool.Scan) is bounded by this even
        // though it never touches MaxFiles.
        public long MaxTotalBytes { get; init; } = BoundedInput.AggregateBytes; // 500 MB — single source of truth (WM-40)

        public int FilesSeen { get; private set; }
        public long BytesAccepted { get; private set; }
        public int FilesSkippedOversized { get; private set; }
        public bool Truncated { get; private set; }

        public bool TryAccept(long fileBytes)
        {
            if (FilesSeen >= MaxFiles)
            {
                Truncated = true;
                return false;
            }
            if (BytesAccepted + fileBytes > MaxTotalBytes)
            {
                Truncated = true;
                return false;
            }
            FilesSeen++;
            BytesAccepted += fileBytes;
            return true;
        }

        internal void RecordOversizedSkip() => FilesSkippedOversized++;

        /// <summary>
        /// SEC-04 — a one-line "scan incomplete" clause when the walk hit a cap or files
        /// were skipped, or <c>null</c> when the scan covered everything. Standalone scan
        /// tools append this to their report so a truncated / partial result is never
        /// presented as complete. <paramref name="skippedUnreadable"/> is the count of
        /// files the caller could not read (locked / raced / out-of-root — WM-18).
        /// </summary>
        public string? IncompleteNote(int skippedUnreadable = 0)
        {
            var clauses = new List<string>();
            if (Truncated)
                clauses.Add($"stopped after {FilesSeen:N0} files (repo-size cap reached)");
            if (FilesSkippedOversized > 0)
                clauses.Add($"skipped {FilesSkippedOversized:N0} oversized file(s) (over the per-file size cap)");
            if (skippedUnreadable > 0)
                clauses.Add($"skipped {skippedUnreadable:N0} unreadable file(s)");
            return clauses.Count == 0 ? null : string.Join("; ", clauses);
        }
    }

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
    ///
    /// Bounded by a default <see cref="ScanBudget"/> — see the 3-argument
    /// overload if the caller wants to know whether the walk was truncated.
    /// </summary>
    public static IEnumerable<string> EnumerateCsFiles(string repoRoot, IReadOnlyList<string>? excludes)
        => EnumerateCsFiles(repoRoot, excludes, new ScanBudget());

    /// <summary>
    /// Same as the two-argument overload, but stops once <paramref name="budget"/>'s
    /// file-count ceiling is reached. Callers that want to surface a "scan
    /// truncated" note (e.g. <c>DoctorTool</c>) should pass their own
    /// <see cref="ScanBudget"/> and inspect it after enumerating.
    /// </summary>
    public static IEnumerable<string> EnumerateCsFiles(string repoRoot, IReadOnlyList<string>? excludes, ScanBudget budget)
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

            long length;
            try { length = new FileInfo(path).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue; // raced/unreadable between enumeration and stat — same skip as IgnoreInaccessible
            }
            if (length > budget.MaxFileBytes)
            {
                budget.RecordOversizedSkip();
                continue; // skip just this one file — the walk keeps going
            }

            if (!budget.TryAccept(length))
                yield break; // file-count or aggregate-byte cap — stop the walk entirely
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
    public static IEnumerable<string> EnumerateCsprojFiles(string repoRoot, ScanBudget? budget = null)
    {
        // SEC-17: the csproj walk had NO count/aggregate cap and — unlike the .cs and
        // generic walks — did not even skip /bin/ + /obj/, so a restored monorepo's
        // thousands of generated project files under obj/ were all enumerated unbounded.
        return ApplyBudget(
            Directory.EnumerateFiles(repoRoot, "*.csproj", RecursiveCsOptions),
            budget ?? new ScanBudget(),
            skipBinObj: true);
    }

    /// <summary>
    /// Enumerates every file under <paramref name="repoRoot"/> matching
    /// <paramref name="searchPattern"/>, using the SAME hardened options as the
    /// <c>.cs</c> walk (no symlinks / hidden / system, inaccessible subdirs skipped)
    /// and excluding <c>/bin/</c> + <c>/obj/</c> artefact paths. For non-<c>.cs</c>
    /// project metadata such as <c>Directory.Packages.props</c>, so those walks don't
    /// reintroduce the raw <c>Directory.EnumerateFiles</c> symlink-following risk.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string repoRoot, string searchPattern, ScanBudget? budget = null)
    {
        // SEC-17: bounded by the same count/aggregate budget as the .cs and csproj walks.
        return ApplyBudget(
            Directory.EnumerateFiles(repoRoot, searchPattern, RecursiveCsOptions),
            budget ?? new ScanBudget(),
            skipBinObj: true);
    }

    /// <summary>
    /// Shared tail for the csproj / generic walks (SEC-17): skips <c>/bin/</c> +
    /// <c>/obj/</c> artefact paths, skips files above <see cref="ScanBudget.MaxFileBytes"/>,
    /// and stops the walk once <paramref name="budget"/>'s count / aggregate-byte
    /// ceiling is reached — mirroring <see cref="EnumerateCsFiles(string, IReadOnlyList{string}?, ScanBudget)"/>.
    /// (EnumerateCsFiles keeps its own inline copy because it also applies the
    /// caller-supplied exclude list before the budget.)
    /// </summary>
    private static IEnumerable<string> ApplyBudget(IEnumerable<string> paths, ScanBudget budget, bool skipBinObj)
    {
        foreach (var path in paths)
        {
            if (skipBinObj)
            {
                var n = path.Replace('\\', '/');
                if (n.Contains("/bin/", StringComparison.Ordinal)) continue;
                if (n.Contains("/obj/", StringComparison.Ordinal)) continue;
            }

            long length;
            try { length = new FileInfo(path).Length; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue; // raced/unreadable between enumeration and stat
            }
            if (length > budget.MaxFileBytes)
            {
                budget.RecordOversizedSkip();
                continue;
            }

            if (!budget.TryAccept(length))
                yield break;
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
        // F-23 — a bare StartsWith(rootFull) has no separator boundary, so
        // /work/repo-evil/file.cs incorrectly passes containment against root
        // /work/repo. Match PathGuard's boundary-safe comparison: equal, or
        // starts with root + separator.
        var rootFull = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fileFull = Path.GetFullPath(file);

        if (fileFull.Equals(rootFull, PathComparison))
            return string.Empty;

        var rootBoundary = rootFull + Path.DirectorySeparatorChar;
        if (fileFull.StartsWith(rootBoundary, PathComparison))
            return fileFull.Substring(rootBoundary.Length).Replace('\\', '/');

        // Non-leaky message: name the parameter class, not the input.
        throw new InvalidOperationException(
            "MakeRelative: file is outside the repository root. " +
            "Caller must validate containment (PathGuard.ValidateContainment) before invoking.");
    }
}
