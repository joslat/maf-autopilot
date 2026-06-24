namespace MafDoctor.Commands;

/// <summary>
/// Argument parsing for the <c>doctor</c> CLI subcommand, extracted from the
/// Program.cs top-level statements so it can be unit-tested without spawning a
/// process (the analysis itself lives in <c>DoctorTool</c>).
///
/// Grammar: <c>doctor [path] [--exclude &lt;substr&gt;]... [--all|--full] [--json|--plan]</c>
/// <list type="bullet">
///   <item>The first non-flag token is the path (default: current directory at
///         the call site — left null here so the caller decides the default).</item>
///   <item><c>--exclude &lt;substr&gt;</c> is repeatable; each skips files whose
///         repo-relative path contains the substring (used by the drift detector
///         to scan product code only).</item>
///   <item><c>--all</c> / <c>--full</c> lists every finding (grouped) vs the top 3.</item>
///   <item><c>--json</c> emits the flat machine-readable findings; <c>--plan</c> emits the
///         human checklist; <c>--plan --json</c> together emit the structured remediation
///         <em>manifest</em> (phased + per-finding confidence) for an automated fix loop.</item>
/// </list>
/// </summary>
internal static class DoctorCli
{
    public static (string? Path, string Format, List<string> Excludes, bool Full) Parse(string[] args)
    {
        var excludes = new List<string>();
        string? path = null;
        var full = false;
        var json = false;
        var plan = false;

        // args[0] is "doctor"; options start at index 1.
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--exclude" && i + 1 < args.Length) { excludes.Add(args[++i]); continue; }
            if (args[i] is "--all" or "--full") { full = true; continue; }
            if (args[i] is "--json") { json = true; continue; }
            if (args[i] is "--plan") { plan = true; continue; }
            // First non-flag token is the path. The `--` guard means a flag that
            // appears before the path (e.g. `doctor --json .`) is never mistaken
            // for the path.
            if (path is null && !args[i].StartsWith("--", StringComparison.Ordinal)) path = args[i];
        }

        // `--plan --json` (either order) = the machine-readable remediation MANIFEST
        // (a structured plan an automated remediation loop can iterate). `--plan` alone
        // is the human checklist; `--json` alone is the flat findings array.
        var format = (plan, json) switch
        {
            (true, true) => "plan-json",
            (true, false) => "plan",
            (false, true) => "json",
            _ => "markdown",
        };

        return (path, format, excludes, full);
    }
}
