using System.Runtime.InteropServices;

namespace MafDoctor.Tools;

/// <summary>
/// MCP-only workspace-root containment. Closes finding F-04 of the 2026-07-19
/// security assessment: <see cref="PathGuard.ValidateRepoPath"/> previously
/// confirmed a `repoPath` was syntactically well-formed and existed, but never
/// checked it belonged to a workspace the MCP client actually granted — an
/// agent-driven tool call could point at any absolute directory the host
/// account can read (another repo, the user profile, a mounted drive).
///
/// This is deliberately MCP-only. A human running the CLI directly (`maf-doctor
/// doctor ~/some/other/repo`) is the trust boundary itself and must keep working
/// against any path they type — <see cref="EnableMcpMode"/> is called from
/// exactly one place in <c>Program.cs</c>, on the branch that starts the MCP
/// server, which every CLI subcommand returns before reaching.
/// </summary>
internal static class WorkspacePolicy
{
    /// <summary>
    /// Semicolon-separated list of absolute directories an MCP-connected agent
    /// is allowed to point tools at. Semicolon (not the platform path-list
    /// separator) so the same value works unescaped in a JSON "env" string on
    /// every OS — ":" collides with Windows drive letters (`C:\...`).
    /// When unset, no explicit allowlist is enforced (back-compat default) but
    /// the always-on filesystem-root/home-directory denial below still applies.
    /// </summary>
    public const string WorkspaceRootsEnvName = "MAF_DOCTOR_WORKSPACE_ROOTS";

    private static readonly StringComparison PathComparison =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static volatile bool _mcpMode;

    /// <summary>Called once, from Program.cs, only on the MCP-server startup path.</summary>
    public static void EnableMcpMode() => _mcpMode = true;

    // Test-only escape hatch: production code has no other way to flip this back
    // off, by design (see class remarks) — tests need to simulate both modes
    // within one process.
    internal static void ResetForTests(bool mcpMode) => _mcpMode = mcpMode;

    // Not cached: reads the env var fresh on every call. The var is read at
    // most once per MCP tool invocation (never in a hot loop), so the cost is
    // negligible, and re-reading keeps this testable without a process restart
    // and lets an operator change the env var and restart just the MCP server
    // (not the whole test/host process) to pick up a new value.
    /// <summary>The configured allowlist, canonicalized. Empty when unset.</summary>
    public static IReadOnlyList<string> ConfiguredRoots => ParseConfiguredRoots();

    private static List<string> ParseConfiguredRoots()
    {
        var raw = Environment.GetEnvironmentVariable(WorkspaceRootsEnvName);
        if (string.IsNullOrWhiteSpace(raw)) return [];

        var roots = new List<string>();
        foreach (var candidate in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                roots.Add(Normalize(Path.GetFullPath(candidate)));
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
            {
                // Malformed entry — skip rather than crash server startup over a typo.
            }
        }
        return roots;
    }

    private static string Normalize(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Returns an error message if <paramref name="path"/> is unsafe under the
    /// current workspace policy, or null when it's OK. A no-op outside MCP mode.
    /// </summary>
    public static string? Validate(string path) => _mcpMode ? ValidateCore(path) : null;

    /// <summary>
    /// The actual containment logic, independent of <see cref="_mcpMode"/>.
    /// Split out so tests can exercise every branch without touching the
    /// process-global mode flag — <see cref="_mcpMode"/> is read by every
    /// concurrently-running test class via <see cref="PathGuard.ValidateRepoPath"/>,
    /// so flipping it in a unit test would be a real (if narrow) source of
    /// cross-test flakiness under xUnit's default parallel execution.
    /// </summary>
    internal static string? ValidateCore(string path)
    {
        string rawFull;
        try
        {
            rawFull = Path.GetFullPath(path);
        }
        catch
        {
            return null; // Path.GetFullPath already validated upstream by PathGuard; be conservative.
        }

        // Always-on regardless of configuration: no legitimate MAF Doctor call
        // targets an entire filesystem root or the user's home directory, and an
        // operator who never configured MAF_DOCTOR_WORKSPACE_ROOTS should not be
        // silently exposed to either just by not opting in.
        //
        // Checked against the RAW (un-normalized) full path, deliberately.
        // A prior version normalized first — TrimEnd-ing every trailing
        // separator off a bare POSIX root "/" collapses it to "", and
        // Path.GetPathRoot("") returns "", silently defeating this exact
        // check for exactly the input it exists to catch. Windows happened
        // to survive that bug (TrimEnd("C:\\") leaves "C:", still non-empty
        // and still equal to Path.GetPathRoot("C:")), which is why it shipped
        // undetected until verified by actually running on Linux. Comparing
        // the untrimmed root against the untrimmed full path sidesteps the
        // collapse entirely on both platforms.
        if (IsFilesystemRoot(rawFull))
            return "Error: repoPath must not be a filesystem root.";

        var full = Normalize(rawFull);

        var home = GetHomeDirectory();
        if (home is not null && full.Equals(home, PathComparison))
            return "Error: repoPath must not be the user home directory.";

        var roots = ConfiguredRoots;
        if (roots.Count == 0) return null; // No explicit allowlist configured — back-compat default.

        foreach (var root in roots)
        {
            if (full.Equals(root, PathComparison) || full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
                return null;
        }

        return $"Error: repoPath is outside the configured MAF Doctor workspace root(s) " +
               $"({string.Join("; ", roots)}). Add it to {WorkspaceRootsEnvName} in the MCP server's " +
               "env config if this is intentional.";
    }

    private static bool IsFilesystemRoot(string rawFull)
    {
        // Compare untrimmed forms — see the call site's comment for why
        // normalizing before this check is exactly the bug that shipped.
        var pathRoot = Path.GetPathRoot(rawFull);
        return !string.IsNullOrEmpty(pathRoot) && rawFull.Equals(pathRoot, PathComparison);
    }

    private static string? GetHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? null : Normalize(home);
    }
}
