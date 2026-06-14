using MafDoctor.Scaffolding;
using MafDoctor.Tools;

namespace MafDoctor;

/// <summary>
/// CLI subcommand: <c>maf-autopilot new agent &lt;Name&gt;</c> or <c>new executor &lt;Name&gt;</c>.
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

        Console.WriteLine($"maf-autopilot new {kind} {name}");
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
        WriteFiles(path, files);
        PrintNextStepsForAgent();
        return 0;
    }

    private static int GenerateExecutor(string path, string name, string[] args)
    {
        var inputType = ExtractFlag(args, "--input") ?? "string";
        var outputType = ExtractFlag(args, "--output") ?? "string";
        var ns = NewAgentTool.DetectNamespace(path, fallback: "MyApp.Workflows");
        var files = AgentScaffolder.ScaffoldExecutor(name, ns, inputType, outputType);
        WriteFiles(path, files);
        PrintNextStepsForExecutor();
        return 0;
    }

    private static void PrintNextStepsForAgent()
    {
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine();
        Console.WriteLine("  0. If your project doesn't reference MAF yet, add the packages:");
        Console.WriteLine("       dotnet add package Microsoft.Agents.AI --version 1.3.0");
        Console.WriteLine("       dotnet add package Microsoft.Extensions.AI --version 10.5.*");
        Console.WriteLine("       dotnet add package Azure.AI.OpenAI");
        Console.WriteLine("       dotnet add package Azure.Identity");
        Console.WriteLine();
        Console.WriteLine("  1. Wire DI: register IChatClient and your new agent in Program.cs.");
        Console.WriteLine("  2. dotnet build && dotnet test");
        Console.WriteLine("  3. Optional: run `maf-autopilot doctor .` to confirm anti-pattern-clean.");
    }

    private static void PrintNextStepsForExecutor()
    {
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine();
        Console.WriteLine("  0. If your project doesn't reference MAF Workflows yet, add the packages:");
        Console.WriteLine("       dotnet add package Microsoft.Agents.AI.Workflows --version 1.3.0");
        Console.WriteLine("       dotnet add package Microsoft.Agents.AI.Workflows.Generators --version 1.3.0");
        Console.WriteLine();
        Console.WriteLine("  1. Wire into your workflow's AddFanOutEdge / AddFanInBarrierEdge calls.");
        Console.WriteLine("  2. dotnet build && dotnet test");
        Console.WriteLine("  3. Optional: run `maf-autopilot doctor .` to confirm fan-out-safe.");
    }

    private static void WriteFiles(string baseDir, IReadOnlyList<AgentScaffolder.ScaffoldedFile> files)
    {
        foreach (var file in files)
        {
            var absolute = Path.Combine(baseDir, file.RelativePath);
            var dir = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (File.Exists(absolute))
            {
                Console.WriteLine($"  ⚠ {file.RelativePath} exists — skipped");
                continue;
            }
            File.WriteAllText(absolute, file.Content);
            var lineCount = file.Content.Split('\n').Length;
            Console.WriteLine($"  ✓ {file.RelativePath} ({lineCount} lines)");
        }
    }

    private static int UnknownKind(string kind)
    {
        Console.Error.WriteLine($"Unknown kind '{kind}'. Expected: agent | executor");
        Console.Error.WriteLine(Usage());
        return 1;
    }

    private static string Usage() => """
        Usage:
          maf-autopilot new agent <Name> [--path <dir>] [--instructions "<prompt>"]
          maf-autopilot new executor <Name> [--path <dir>] [--input <type>] [--output <type>]

        Examples:
          maf-autopilot new agent ChatBot
          maf-autopilot new executor FraudReviewer --input FraudCheck --output FraudReport
        """;

    private static string? ExtractFlag(string[] args, string flag)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }
}
