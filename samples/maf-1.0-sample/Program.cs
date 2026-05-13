// SPDX-License-Identifier: MIT
// Copyright (c) 2026 maf-autopilot sample.
//
// FraudClaimsTriage — a multi-agent MAF 1.3.0 workflow that deliberately
// embeds the 1.3-era anti-patterns the maf-autopilot toolkit was built to find
// and fix. Pinned to MAF 1.3.0 exactly so the migration toolkit can be
// dogfooded end-to-end (Phase T → A.8 unblocker).
//
// Run modes:
//   dotnet run                   # dry-run — build the workflow + print structure
//   dotnet run -- --run          # also call the LLM (requires AZURE_OPENAI_* env vars)
//
// What this codebase models:
//   IntakeAgent  →  (fan-out)  →  Osint, History, Transactions
//                             →  (fan-in)
//                                   ↓
//                              Aggregator → DecisionAgent → Notification

using System.Text;
using MafSample.FraudClaims;
using MafSample.FraudClaims.Workflows;

Console.OutputEncoding = Encoding.UTF8;

PrintBanner();

var dryRun = !(args.Length > 0 && args[0] == "--run");

if (dryRun)
{
    Console.WriteLine("  Mode: DRY-RUN (no LLM calls).");
    Console.WriteLine("  Pass --run to also invoke Azure OpenAI (requires env vars).");
    Console.WriteLine();
}

// Build the chat client even in dry-run mode — the maf-autopilot scanners
// inspect the SOURCE not the runtime, and we want the construction code path
// to be present and discoverable by the scanner.
var chatClient = ChatClientFactory.CreateChatClient();
var workflow = FraudClaimsWorkflow.Create(chatClient);

Console.WriteLine($"  ✓ Built workflow.");
Console.WriteLine("  ✓ Topology: Intake → fan-out(Osint|History|Transactions) → Aggregator → Decision → Notification");
Console.WriteLine();
Console.WriteLine("  Anti-patterns embedded in this sample (see README.md):");

var embeddedAntiPatterns = new (string Id, string Where)[]
{
    ("MAF-AP-SEC-001 / MAF002",  "ChatClientFactory.cs           — DefaultAzureCredential"),
    ("MAF-AP-SEC-002",           "ChatClientFactory.cs           — hardcoded api-key literal"),
    ("MAF-AP-SEC-003 / MAF003",  "ChatClientFactory.cs           — EnableSensitiveData = true"),
    ("MAF-AP-OBS-001",           "ChatClientFactory.cs           — no UseOpenTelemetry()"),
    ("MAF-AP-CONC-001",          "Providers/CaseContextProvider  — instance fields on AIContextProvider"),
    ("MAF-AP-CONC-002",          "Executors/NotificationExecutor — sync-over-async .Result"),
    ("MAF-AP-WF-001",            "Executors/TransactionInvestigator — missing `sealed`"),
    ("MAF130-EXEC-001 / MAF001", "Executors/{Osint,History,Transaction}Investigator — non-generic ValueTask"),
    ("MAF130-FAN-IN-001",        "Workflows/FraudClaimsWorkflow  — obsolete AddFanInBarrierEdge overload (CS0618)"),
    ("MAF130-MIDDLEWARE-001",    "ChatClientFactory              — Use() with null streaming branch"),
};

foreach (var (id, where) in embeddedAntiPatterns)
    Console.WriteLine($"     • {id,-28}  {where}");

Console.WriteLine();

if (!dryRun)
{
    if (!Config.IsConfigured)
    {
        Console.WriteLine("  ⚠ --run requested but AZURE_OPENAI_* env vars are not set; skipping LLM call.");
        return 0;
    }

    Console.WriteLine("  (the --run path is intentionally a no-op stub for now — the sample");
    Console.WriteLine("   is meant to exercise the migration toolkit on the SOURCE, not at runtime.)");
}

return 0;

static void PrintBanner()
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("  ╔══════════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("  ║   MAF 1.3.0 FraudClaimsTriage Sample — Migration Dogfood Fixture     ║");
    Console.WriteLine("  ║   Deliberate anti-patterns; consumed by the maf-autopilot toolkit    ║");
    Console.WriteLine("  ╚══════════════════════════════════════════════════════════════════════╝");
    Console.ResetColor();
    Console.WriteLine();
}
