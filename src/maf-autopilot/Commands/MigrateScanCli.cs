namespace MafDoctor.Commands;

/// <summary>
/// Argument parsing for the <c>migrate-scan</c> CLI subcommand, extracted from the
/// Program.cs top-level statements so the flag handling is unit-testable without
/// spawning a process (mirrors <see cref="DoctorCli"/> / <see cref="AutoFixCli"/>).
///
/// Grammar: <c>migrate-scan [path] [--source &lt;v&gt;|--source=&lt;v&gt;] [--json]</c>
/// <list type="bullet">
///   <item>The first non-flag token is the path (default decided by the caller).</item>
///   <item><c>--source</c> takes the source framework, in space (<c>--source sk</c>) or
///         equals (<c>--source=sk</c>) form; default <c>semantic-kernel</c>. The
///         <c>--source</c> VALUE is consumed here so it is never mistaken for the path.</item>
///   <item><c>--json</c> emits the machine-readable inventory.</item>
/// </list>
/// </summary>
internal static class MigrateScanCli
{
    public const string DefaultSource = "semantic-kernel";

    public static (string? Path, string Source, bool Json) Parse(string[] args)
    {
        string? path = null;
        var source = DefaultSource;
        var json = false;

        // args[0] is "migrate-scan"; options start at index 1.
        for (int i = 1; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "--json") { json = true; continue; }
            // Space form: consume the NEXT token as the value so it is never read as
            // the path (`migrate-scan --source sk C:/repo` must keep C:/repo as path).
            // Read then advance `i` as separate statements so the token-consumption is visible
            // (vs. an `args[++i]` that mutates the loop counter from inside the subscript).
            // Only consume the next token when it is a VALUE, not another flag — otherwise
            // `migrate-scan --source --json` would eat `--json` as the source and never enable it.
            if (a == "--source")
            {
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    source = args[i + 1];
                    i++;
                }
                continue;
            }
            // Equals form: `--source=sk`.
            if (a.StartsWith("--source=", StringComparison.Ordinal)) { source = a["--source=".Length..]; continue; }
            // First non-flag token is the path; any other --flag is ignored here.
            if (path is null && !a.StartsWith("--", StringComparison.Ordinal)) path = a;
        }

        return (path, source, json);
    }

    /// <summary>
    /// Is this a source framework the migrate-scan flow currently supports? Delegates
    /// to the shared <see cref="MigrationSources"/> allow-list so the CLI, the
    /// <c>maf://migrate-from</c> resource, and the <c>maf-migrate-from</c> prompt can
    /// never disagree. Only Semantic Kernel today (alias <c>sk</c>); AutoGen is a later phase.
    /// </summary>
    public static bool IsSupportedSource(string source) => MigrationSources.IsSupported(source);
}
