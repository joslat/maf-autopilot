// SPDX-License-Identifier: MIT
//
// ⚠️ Same anti-pattern as OsintInvestigator — non-generic ValueTask return.
// Three fan-out executors all making the same mistake = three branches
// silently producing no fan-in input.
//
// Triggers:
//   - MAF130-EXEC-001 (registry)
//   - MAF001          (analyzer)

using Microsoft.Agents.AI.Workflows;

namespace MafSample.FraudClaims.Executors;

public sealed partial class HistoryInvestigator : Executor
{
    public HistoryInvestigator() : base(id: "History") { }

    [MessageHandler]
    // ⚠️ MAF130-EXEC-001 — should return ValueTask<InvestigationFinding>.
    public async ValueTask HandleAsync(ClaimInput claim, IWorkflowContext context, CancellationToken ct)
    {
        await Task.Yield();
        var finding = new InvestigationFinding(
            Investigator: "History",
            RiskScore: 0.42,
            Notes: $"Customer {claim.CustomerId} has 2 prior claims in the last 18 months.");
        _ = finding;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder) => builder;
}
