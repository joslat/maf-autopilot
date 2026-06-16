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
          doctor [path] [--exclude <s>]...  Health report with an A/B/C/F grade.
          autofix-all [path] [--dry-run]    Apply deterministic Roslyn fixes.
          new agent <Name>                  Scaffold a MAF agent + smoke test.
          new executor <Name> [In] [Out]    Scaffold a workflow executor + smoke test.
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
    // Usage: maf-doctor doctor [path] [--exclude <substr>]...
    // Repeatable `--exclude` skips files whose repo-relative path contains the
    // substring — used by the drift detector to scan product code only (skip
    // samples/ + test fixtures). The first non-flag arg is the path (default cwd).
    var excludes = new List<string>();
    string? path = null;
    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] == "--exclude" && i + 1 < args.Length) { excludes.Add(args[++i]); continue; }
        if (path is null && !args[i].StartsWith("--", StringComparison.Ordinal)) path = args[i];
    }
    path ??= Directory.GetCurrentDirectory();
    var report = new MafDoctor.Tools.DoctorTool().Run(path, "markdown", excludes);
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
    // Copilot Chat. Usage: `maf-doctor autofix-all [path] [--dry-run]`.
    var positional = args.Skip(1).Where(a => !a.StartsWith("--")).ToList();
    var path = positional.Count > 0 ? positional[0] : Directory.GetCurrentDirectory();
    var dryRun = args.Contains("--dry-run");
    var report = new MafDoctor.Tools.AutoFixTool().MafAutoFixAll(path, dryRun: dryRun);
    Console.WriteLine(report);
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
