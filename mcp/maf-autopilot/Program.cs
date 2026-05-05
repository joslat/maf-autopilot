using MafAutopilot;
using MafAutopilot.Data;
using MafAutopilot.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// 'maf-autopilot init' — CLI mode: configure a target repo for MAF migration.
// Must be checked before Host.CreateApplicationBuilder so stdin/stdout remain clean.
if (args.Length > 0 && args[0] == "init")
{
    var exitCode = await InitCommand.RunAsync();
    Environment.Exit(exitCode);
    return; // unreachable; satisfies compiler
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
