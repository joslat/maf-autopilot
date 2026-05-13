using MafAutopilot;
using MafAutopilot.Data;
using MafAutopilot.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// CLI mode: short-circuit before Host.CreateApplicationBuilder so stdin/stdout remain clean.
// Supported subcommands:
//   maf-autopilot init                       — scaffold .vscode/mcp.json + copilot-instructions
//   maf-autopilot new agent <Name>           — generate a MAF 1.3.0 agent + smoke test
//   maf-autopilot new executor <Name> [In] [Out]  — generate a workflow executor + smoke test
//   maf-autopilot doctor [path]              — one-command repo health report (A/B/C/F grade)
if (args.Length > 0 && args[0] == "init")
{
    var exitCode = await InitCommand.RunAsync();
    Environment.Exit(exitCode);
    return; // unreachable; satisfies compiler
}
if (args.Length >= 2 && args[0] == "new")
{
    var exitCode = NewCommand.Run(args);
    Environment.Exit(exitCode);
    return;
}
if (args.Length >= 1 && args[0] == "doctor")
{
    var path = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();
    var report = new MafAutopilot.Tools.DoctorTool().MafDoctor(path);
    Console.WriteLine(report);
    Environment.Exit(0);
    return;
}
if (args.Length >= 1 && args[0] == "badge")
{
    // Phase W.13 — emit a shields.io endpoint-badge JSON payload. Consumers
    // host the JSON output (X.4) and reference it from shields.io URLs like
    // https://img.shields.io/endpoint?url=<your-hosted-json>.
    var path = args.Length >= 2 ? args[1] : Directory.GetCurrentDirectory();
    var badge = MafAutopilot.Commands.BadgeCommand.Build(path);
    Console.WriteLine(badge);
    Environment.Exit(0);
    return;
}
if (args.Length >= 1 && args[0] == "autofix-all")
{
    // Phase W bonus + #8 enabler — surface MafAutoFixAll on the CLI so the
    // migration-cast.tape can drive the auto-fix flow without going through
    // Copilot Chat. Usage: `maf-autopilot autofix-all [path] [--dry-run]`.
    var positional = args.Skip(1).Where(a => !a.StartsWith("--")).ToList();
    var path = positional.Count > 0 ? positional[0] : Directory.GetCurrentDirectory();
    var dryRun = args.Contains("--dry-run");
    var report = new MafAutopilot.Tools.AutoFixTool().MafAutoFixAll(path, dryRun: dryRun);
    Console.WriteLine(report);
    Environment.Exit(0);
    return;
}
if (args.Length >= 1 && args[0] == "verify-registry")
{
    // #5 polish (2026-05-13) — verification gate for the AI-fill flow. The
    // PR-triggered workflow maf-ai-fill-verify.yml runs this command against
    // the AI-filled registry.yaml. Exit code 1 blocks the PR's auto-merge.
    var exitCode = MafAutopilot.Commands.VerifyRegistryCommand.Run();
    Environment.Exit(exitCode);
    return;
}
if (args.Length >= 1 && args[0] == "registry-extract")
{
    var exitCode = RegistryExtractCommand.Run(args);
    Environment.Exit(exitCode);
    return;
}

// All logs must go to stderr — stdout is reserved for the MCP JSON-RPC protocol.
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Load the registry once at startup and register as a singleton shared by all tools.
builder.Services
    .AddSingleton<RegistryService>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly)
    .WithResourcesFromAssembly(typeof(Program).Assembly)
    .WithPromptsFromAssembly(typeof(Program).Assembly);

await builder.Build().RunAsync();
