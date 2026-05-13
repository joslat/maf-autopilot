// SPDX-License-Identifier: MIT
//
// ⚠️ DELIBERATE ANTI-PATTERN: sync-over-async via `.Result`. Blocks the
// scheduler thread; in an MCP/agent workflow it can deadlock under SyncContext.
//
// Triggers:
//   - MAF-AP-CONC-002 (anti-pattern scanner)

using Microsoft.Agents.AI.Workflows;

namespace MafSample.FraudClaims.Executors;

public sealed partial class NotificationExecutor : Executor
{
    public NotificationExecutor() : base(id: "Notification") { }

    [MessageHandler]
    public async ValueTask<TriageDecision> HandleAsync(TriageDecision decision, IWorkflowContext context, CancellationToken ct)
    {
        await Task.Yield();
        // ⚠️ MAF-AP-CONC-002 — blocking on .Result inside an async pipeline.
        var ack = SendNotificationAsync(decision).Result;
        Console.WriteLine($"  📨 Notification: {ack}");
        return decision;
    }

    private static Task<string> SendNotificationAsync(TriageDecision decision)
        => Task.FromResult($"Sent for {decision.ClaimId}: {decision.Action}");

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder) => builder;
}
