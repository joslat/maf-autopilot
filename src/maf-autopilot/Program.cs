using System.Reflection;
using MafDoctor;
using MafDoctor.Data;
using MafDoctor.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

// CLI mode: short-circuit before Host.CreateApplicationBuilder so stdin/stdout remain clean.
// Supported subcommands:
//   maf-doctor init                       — scaffold .vscode/mcp.json + copilot-instructions
//   maf-doctor new agent <Name>           — generate a MAF agent + smoke test
//   maf-doctor new executor <Name> [In] [Out]  — generate a workflow executor + smoke test
//   maf-doctor doctor [path]              — one-command repo health report (A/B/C/F grade)
// --version / --help — short-circuit before anything else (and before the
// no-subcommand fall-through to MCP-server mode, which would otherwise hang
// waiting on stdio when a user runs `maf-doctor --version`).
if (args.Length > 0 && (args[0] is "--version" or "-v" or "version"))
{
    var raw = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";
    // InformationalVersion can carry build metadata (e.g. "1.2.7+abc123") — trim it.
    var plus = raw.IndexOf('+');
    Console.WriteLine($"maf-doctor {(plus >= 0 ? raw[..plus] : raw)}");
    Environment.Exit(0);
    return;
}
if (args.Length > 0 && (args[0] is "--help" or "-h" or "help"))
{
    Console.WriteLine(
        """
        maf-doctor — a toolkit for Microsoft Agent Framework (MAF)

        Usage: maf-doctor [command] [options]

        With NO command, runs as an MCP server over stdio (for VS Code / Claude Code / Cursor).

        Commands:
          init [--with-cursor]              Wire the MCP server + steering into the current repo
                                            (.vscode/mcp.json, .mcp.json, instructions, agents).
          doctor [path] [--exclude <s>] [--all|--full] [--json] [--plan]
                                            A/B/C/F health report. --all = every finding (grouped),
                                            not just the top 3; --json = machine-readable findings;
                                            --plan = ordered, checkboxed remediation plan;
                                            --plan --json = structured remediation manifest (for an automated fix loop).
          autofix-all [path] [--dry-run] [--json]
                                            Apply deterministic Roslyn fixes. Human-readable summary
                                            by default; --dry-run = preview only (no writes);
                                            --json = machine-readable output.
          new agent <Name>                  Scaffold a MAF agent + smoke test.
          new executor <Name> [In] [Out]    Scaffold a workflow executor + smoke test.
          migrate-scan [path] [--json]      Inventory Semantic Kernel usage and tag each construct
                                            by migration strategy (bridgeable / rewrite / re-architect)
                                            + a complexity verdict. (--source semantic-kernel)
          badge [path]                      Emit a shields.io health-badge JSON payload.
          verify-registry                   Validate the obsolete-API registry (CI gate).
          registry-extract                  Extract registry entries (CI helper).
          --version, -v                     Print the installed version.
          --help, -h                        Show this help.

        Docs: https://github.com/joslat/maf-doctor
        """);
    Environment.Exit(0);
    return;
}
if (args.Length > 0 && args[0] == "init")
{
    var exitCode = await InitCommand.RunAsync(args);
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
    // Usage: maf-doctor doctor [path] [--exclude <substr>]... [--all|--full] [--json]
    // Parsing lives in DoctorCli.Parse (extracted so it's unit-testable without a
    // process spawn). `--exclude` skips files by repo-relative substring (drift
    // detector scopes to product code); `--all`/`--full` lists every finding
    // (grouped); `--json` emits machine-readable output.
    var (parsedPath, format, excludes, full) = MafDoctor.Commands.DoctorCli.Parse(args);
    var path = parsedPath ?? Directory.GetCurrentDirectory();
    var report = new MafDoctor.Tools.DoctorTool().Run(path, format, excludes, full);
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
    var badge = MafDoctor.Commands.BadgeCommand.Build(path);
    Console.WriteLine(badge);
    Environment.Exit(0);
    return;
}
if (args.Length >= 1 && args[0] == "autofix-all")
{
    // Phase W bonus + #8 enabler — surface MafAutoFixAll on the CLI so the
    // migration-cast.tape can drive the auto-fix flow without going through
    // Copilot Chat. Usage: `maf-doctor autofix-all [path] [--dry-run] [--json]`.
    // Default output is human-readable; `--json` emits the machine-readable
    // (MCP-tool) shape. Parsing lives in AutoFixCli so it's unit-testable.
    var (parsedPath, json, dryRun) = MafDoctor.Commands.AutoFixCli.Parse(args);
    var path = parsedPath ?? Directory.GetCurrentDirectory();
    var tool = new MafDoctor.Tools.AutoFixTool();
    if (json)
    {
        // Machine-readable: identical to the MafAutoFixAll MCP tool output.
        Console.WriteLine(tool.MafAutoFixAll(path, dryRun: dryRun));
    }
    else
    {
        var report = tool.RunAll(path, specificFile: null, dryRun: dryRun, out var error);
        Console.WriteLine(error is not null
            ? $"❌ {error}"
            : MafDoctor.Commands.AutoFixCli.Format(report!));
    }
    Environment.Exit(0);
    return;
}
if (args.Length >= 1 && args[0] == "migrate-scan")
{
    // Cross-framework migration — Phase 1. Inventory Semantic Kernel usage and tag
    // each construct by migration strategy (bridgeable / rewrite / re-architect).
    // Usage: `maf-doctor migrate-scan [path] [--source semantic-kernel] [--json]`.
    // Parsing lives in MigrateScanCli so the flag handling is unit-testable and so the
    // `--source` value is never mistaken for the path (in either `--source v` form).
    var (parsedPath, source, json) = MafDoctor.Commands.MigrateScanCli.Parse(args);
    var path = parsedPath ?? Directory.GetCurrentDirectory();
    // --source is currently always semantic-kernel (AutoGen is a later phase); validate
    // the flag so the surface is forward-compatible and an unknown source fails loudly.
    if (!MafDoctor.Commands.MigrateScanCli.IsSupportedSource(source))
    {
        Console.Error.WriteLine($"Only --source semantic-kernel (alias sk) is supported today (got '{source}').");
        Environment.Exit(2);
        return;
    }
    var fmt = json ? "json" : "markdown";
    Console.WriteLine(new MafDoctor.Tools.SemanticKernelDetectorTool().MafDetectSourceFramework(path, fmt));
    Environment.Exit(0);
    return;
}
if (args.Length >= 1 && args[0] == "verify-registry")
{
    // #5 polish (2026-05-13) — verification gate for the AI-fill flow. The
    // PR-triggered workflow maf-ai-fill-verify.yml runs this command against
    // the AI-filled registry.yaml. Exit code 1 blocks the PR's auto-merge.
    var exitCode = MafDoctor.Commands.VerifyRegistryCommand.Run();
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
    .AddHostedService<UpdateNoticeHostedService>()
    .AddMcpServer(options =>
    {
        // Advertise the server's identity + icon (MCP 2025-11 `icons` on the
        // Implementation). Clients that support it render the icon; older clients
        // ignore it. Source points at the asset on `main` so it tracks the brand.
        var v = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        var plus = v.IndexOf('+');
        if (plus >= 0) v = v[..plus];
        options.ServerInfo = new Implementation
        {
            Name = "maf-doctor",
            Title = "MAF Doctor",
            Version = v,
            WebsiteUrl = "https://github.com/joslat/maf-doctor",
            Icons =
            [
                new Icon
                {
                    Source = "https://raw.githubusercontent.com/joslat/maf-doctor/main/assets/MAFDoctorIcon.png",
                    MimeType = "image/png",
                    Sizes = ["any"],
                },
            ],
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly(typeof(Program).Assembly)
    .WithResourcesFromAssembly(typeof(Program).Assembly)
    .WithPromptsFromAssembly(typeof(Program).Assembly);

await builder.Build().RunAsync();
