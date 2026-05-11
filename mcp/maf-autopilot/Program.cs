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
