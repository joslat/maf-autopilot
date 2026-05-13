// SPDX-License-Identifier: MIT
//
// ⚠️ DELIBERATE ANTI-PATTERNS:
//   1. Non-generic ValueTask return (MAF130-EXEC-001 / MAF001)
//   2. Missing `sealed` modifier on the Executor class (MAF-AP-WF-001 partial)
//
// The 1.3.0 source generator emits a partial dispatcher; it works without
// `sealed` but the analyzer flags the missing modifier because the canonical
// pattern is `public sealed partial class`. This is a soft warning, not a
// build break — perfect for a sample.

using Microsoft.Agents.AI.Workflows;

namespace MafSample.FraudClaims.Executors;

// ⚠️ MAF-AP-WF-001 — Executor class missing `sealed` modifier.
public partial class TransactionInvestigator : Executor
{
    public TransactionInvestigator() : base(id: "Transactions") { }

    [MessageHandler]
    // ⚠️ MAF130-EXEC-001 — should return ValueTask<InvestigationFinding>.
    public async ValueTask HandleAsync(ClaimInput claim, IWorkflowContext context, CancellationToken ct)
    {
        await Task.Yield();
        var finding = new InvestigationFinding(
            Investigator: "Transactions",
            RiskScore: 0.71,
            Notes: $"Three large refund requests on customer {claim.CustomerId} in last 30 days.");
        _ = finding;
    }

    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder) => builder;
}
