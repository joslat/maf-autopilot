using MafDoctor.Scaffolding;
using MafDoctor.Tools;

namespace MafDoctor;

/// <summary>
/// CLI subcommand: <c>maf-doctor new agent &lt;Name&gt;</c> or <c>new executor &lt;Name&gt;</c>.
/// Operates against the current working directory by default; pass <c>--path &lt;dir&gt;</c>
/// to target a different project.
/// </summary>
internal static class NewCommand
{
    public static int Run(string[] args)
    {
        // args = ["new", "agent", "ChatBot", ...] or ["new", "executor", "FraudReviewer", ...]
        if (args.Length < 3)
        {
            Console.Error.WriteLine(Usage());
            return 1;
        }

        var kind = args[1].ToLowerInvariant();
        var name = args[2];
        var path = ExtractFlag(args, "--path") ?? Directory.GetCurrentDirectory();

        Console.WriteLine($"maf-doctor new {kind} {name}");
        Console.WriteLine($"  Target: {path}");
        Console.WriteLine();

        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Error: directory does not exist: {path}");
            return 2;
        }
        if (!NewAgentTool.IsSafeIdentifier(name))
        {
            Console.Error.WriteLine($"Error: '{name}' is not a valid PascalCase identifier.");
            return 2;
        }

        try
        {
            return kind switch
            {
                "agent" => GenerateAgent(path, name, args),
                "executor" => GenerateExecutor(path, name, args),
                _ => UnknownKind(kind),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during scaffold: {ex.Message}");
            return 3;
        }
    }

    private static int GenerateAgent(string path, string name, string[] args)
    {
        var instructions = ExtractFlag(args, "--instructions");
        var ns = NewAgentTool.DetectNamespace(path, fallback: "MyApp.Agents");
        var files = AgentScaffolder.ScaffoldAgent(name, ns, instructions);
        var created = WriteFiles(path, files);
        if (created < 0) return 3; // WM-06: whole scaffold refused — do NOT print "Next steps".
        if (created > 0) PrintNextStepsForAgent();
        else Console.Error.WriteLine("  (all target files already existed — nothing scaffolded)");
        return 0;
    }

    private static int GenerateExecutor(string path, string name, string[] args)
    {
        // WM-05: the help advertises `new executor <Name> [In] [Out]` positionals —
        // honor them as a fallback to the --input/--output flags (flags win when both given).
        var positional = PositionalsAfter(args, startIndex: 3, "--path", "--input", "--output", "--instructions");
        var inputType = ExtractFlag(args, "--input") ?? positional.ElementAtOrDefault(0) ?? "string";
        var outputType = ExtractFlag(args, "--output") ?? positional.ElementAtOrDefault(1) ?? "string";
        var ns = NewAgentTool.DetectNamespace(path, fallback: "MyApp.Workflows");
        var files = AgentScaffolder.ScaffoldExecutor(name, ns, inputType, outputType);
        var created = WriteFiles(path, files);
        if (created < 0) return 3; // WM-06
        if (created > 0) PrintNextStepsForExecutor();
        else Console.Error.WriteLine("  (all target files already existed — nothing scaffolded)");
        return 0;
    }

    private static void PrintNextStepsForAgent()
    {
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine();
        Console.WriteLine("  0. If your project doesn't reference MAF yet, add the packages:");
        Console.WriteLine("       dotnet add package Microsoft.Agents.AI");
        Console.WriteLine("       dotnet add package Microsoft.Extensions.AI --version 10.5.*");
        Console.WriteLine("       dotnet add package Azure.AI.OpenAI");
        Console.WriteLine("       dotnet add package Azure.Identity");
        Console.WriteLine();
        Console.WriteLine("  1. Wire DI: register IChatClient and your new agent in Program.cs.");
        Console.WriteLine("  2. dotnet build && dotnet test");
        Console.WriteLine("  3. Optional: run `maf-doctor doctor .` to confirm anti-pattern-clean.");
    }

    private static void PrintNextStepsForExecutor()
    {
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine();
        Console.WriteLine("  0. If your project doesn't reference MAF Workflows yet, add the packages:");
        Console.WriteLine("       dotnet add package Microsoft.Agents.AI.Workflows");
        Console.WriteLine("       dotnet add package Microsoft.Agents.AI.Workflows.Generators");
        Console.WriteLine();
        Console.WriteLine("  1. Wire into your workflow's AddFanOutEdge / AddFanInBarrierEdge calls.");
        Console.WriteLine("  2. dotnet build && dotnet test");
        Console.WriteLine("  3. Optional: run `maf-doctor doctor .` to confirm fan-out-safe.");
    }

    /// <summary>
    /// Writes the scaffold. Returns the number of files actually created, or
    /// <c>-1</c> if the WHOLE scaffold was refused by the pre-flight containment
    /// check. WM-06: the caller uses this to avoid printing "Next steps" (and to
    /// exit non-zero) when nothing was scaffolded. Refusal/skip warnings go to
    /// stderr so stdout carries only the created-file list.
    /// </summary>
    private static int WriteFiles(string baseDir, IReadOnlyList<AgentScaffolder.ScaffoldedFile> files)
    {
        // F-03 — this CLI path bypasses NewAgentTool entirely, so it needs its own
        // containment + symlink guard (SafeWorkspaceWriter.TryCreateNew rejects a
        // symlinked Agents/Workflows/Tests directory or a symlinked target file; it
        // preserves the existing skip-on-exist behavior via a secure CreateNew).
        var root = Path.GetFullPath(baseDir);

        // Round-2 review fixup — mirror NewAgentTool.WriteAndReport's pre-flight:
        // validate every target's containment/symlink-safety BEFORE writing any
        // of them, so a symlinked Agents/ dir refuses the WHOLE scaffold instead
        // of leaving a confusing half-generated result (e.g. Tests/BotTests.cs
        // referencing a Bot.cs that was never written). This CLI path had the
        // same one-file-at-a-time gap the MCP path was already fixed for.
        foreach (var file in files)
        {
            var absolute = Path.Combine(root, file.RelativePath);
            try
            {
                PathGuard.ValidateContainment(root, absolute, nameof(file.RelativePath));
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"  ⚠ {file.RelativePath} — refused: {ex.Message}");
                Console.Error.WriteLine("  No files were written — refusing the whole scaffold rather than leaving a partially-generated result.");
                return -1;
            }
        }

        var created = 0;
        foreach (var file in files)
        {
            var absolute = Path.Combine(root, file.RelativePath);
            bool ok;
            try
            {
                ok = SafeWorkspaceWriter.TryCreateNew(root, absolute, file.Content);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"  ⚠ {file.RelativePath} — refused: {ex.Message}");
                continue;
            }

            if (!ok)
            {
                Console.Error.WriteLine($"  ⚠ {file.RelativePath} exists — skipped");
                continue;
            }
            var lineCount = file.Content.Split('\n').Length;
            Console.WriteLine($"  ✓ {file.RelativePath} ({lineCount} lines)");
            created++;
        }
        return created;
    }

    /// <summary>
    /// Collects the non-flag positional tokens at or after <paramref name="startIndex"/>,
    /// skipping any value-taking flag together with its consumed value. Used by WM-05 to
    /// read the advertised `new executor &lt;Name&gt; [In] [Out]` positionals.
    /// </summary>
    internal static List<string> PositionalsAfter(string[] args, int startIndex, params string[] valueFlags)
    {
        var result = new List<string>();
        var valueFlagSet = new HashSet<string>(valueFlags, StringComparer.OrdinalIgnoreCase);
        for (var i = startIndex; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                if (valueFlagSet.Contains(a) && i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    i++; // skip the flag's value too
                continue;
            }
            result.Add(a);
        }
        return result;
    }

    private static int UnknownKind(string kind)
    {
        Console.Error.WriteLine($"Unknown kind '{kind}'. Expected: agent | executor");
        Console.Error.WriteLine(Usage());
        return 1;
    }

    private static string Usage() => """
        Usage:
          maf-doctor new agent <Name> [--path <dir>] [--instructions "<prompt>"]
          maf-doctor new executor <Name> [In] [Out] [--path <dir>] [--input <type>] [--output <type>]
            (In/Out may be given positionally OR via --input/--output; flags win when both are present.)

        Examples:
          maf-doctor new agent ChatBot
          maf-doctor new executor FraudReviewer FraudCheck FraudReport
          maf-doctor new executor FraudReviewer --input FraudCheck --output FraudReport
        """;

    private static string? ExtractFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
