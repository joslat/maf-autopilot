namespace MafDoctor.Commands;

/// <summary>
/// Argument parsing for the <c>doctor</c> CLI subcommand, extracted from the
/// Program.cs top-level statements so it can be unit-tested without spawning a
/// process (the analysis itself lives in <c>DoctorTool</c>).
///
/// Grammar: <c>doctor [path] [--exclude &lt;substr&gt;]... [--all|--full] [--json]</c>
/// <list type="bullet">
///   <item>The first non-flag token is the path (default: current directory at
///         the call site — left null here so the caller decides the default).</item>
///   <item><c>--exclude &lt;substr&gt;</c> is repeatable; each skips files whose
///         repo-relative path contains the substring (used by the drift detector
///         to scan product code only).</item>
///   <item><c>--all</c> / <c>--full</c> lists every finding (grouped) vs the top 3.</item>
///   <item><c>--json</c> emits machine-readable output instead of markdown.</item>
/// </list>
/// </summary>
internal static class DoctorCli
{
    public static (string? Path, string Format, List<string> Excludes, bool Full) Parse(string[] args)
    {
        var excludes = new List<string>();
        string? path = null;
        var full = false;
        var format = "markdown";

        // args[0] is "doctor"; options start at index 1.
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--exclude" && i + 1 < args.Length) { excludes.Add(args[++i]); continue; }
            if (args[i] is "--all" or "--full") { full = true; continue; }
            if (args[i] is "--json") { format = "json"; continue; }
            // First non-flag token is the path. The `--` guard means a flag that
            // appears before the path (e.g. `doctor --json .`) is never mistaken
            // for the path.
            if (path is null && !args[i].StartsWith("--", StringComparison.Ordinal)) path = args[i];
        }

        return (path, format, excludes, full);
    }
}
