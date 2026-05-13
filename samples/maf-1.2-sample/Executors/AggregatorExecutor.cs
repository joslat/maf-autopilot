// SPDX-License-Identifier: MIT
//
// Fan-in barrier target. Collects the 3 investigation findings and emits
// an AggregatedEvidence record. This file itself is CORRECT — the bug is in
// how Workflow wires the fan-in barrier (see Workflows/FraudClaimsWorkflow.cs,
// which uses the wrong argument order on AddFanInBarrierEdge).

using Microsoft.Agents.AI.Workflows;

namespace MafSample.FraudClaims.Executors;

public sealed partial class AggregatorExecutor : Executor
{
    public AggregatorExecutor() : base(id: "Aggregator") { }

    [MessageHandler]
    public async ValueTask<AggregatedEvidence> HandleAsync(
        IEnumerable<InvestigationFinding> findings,
        IWorkflowContext context,
        CancellationToken ct)
    {
        await Task.Yield();
        return new AggregatedEvidence(
            ClaimId: "(set-upstream)",
            Findings: findings.ToList());
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder) => builder;
}
