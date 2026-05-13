// SPDX-License-Identifier: MIT
// Snippet 2 — fan-out investigator. Spot the bug. (~20 lines)

using Microsoft.Agents.AI.Workflows;

namespace FraudClaims.Executors;

public sealed partial class OsintInvestigator : Executor
{
    public OsintInvestigator() : base(id: "Osint") { }

    [MessageHandler]
    public async ValueTask HandleAsync(ClaimInput claim, IWorkflowContext context, CancellationToken ct)
    {
        await Task.Yield();
        var finding = new InvestigationFinding(
            Investigator: "OSINT",
            RiskScore: 0.18,
            Notes: $"No adverse media for customer {claim.CustomerId}.");
        _ = finding;
    }
}
