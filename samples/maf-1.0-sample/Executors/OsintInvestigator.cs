// SPDX-License-Identifier: MIT
//
// ⚠️ DELIBERATE ANTI-PATTERN: fan-out [MessageHandler] returns non-generic
// ValueTask. In a fan-out → fan-in topology, the executor produces NO output
// message to the fan-in barrier — the barrier never sees this branch's
// contribution and the workflow completes "successfully" with missing data.
//
// CORRECT pattern: return ValueTask<InvestigationFinding> (or Task<T> /
// IAsyncEnumerable<T>) so the message bus has something to route.
//
// Triggers:
//   - MAF130-EXEC-001 (registry — silent runtime failure)
//   - MAF001          (Roslyn analyzer — write-time enforcement)

using Microsoft.Agents.AI.Workflows;

namespace MafSample.FraudClaims.Executors;

public sealed partial class OsintInvestigator : Executor
{
    public OsintInvestigator() : base(id: "Osint") { }

    [MessageHandler]
    // ⚠️ MAF130-EXEC-001 — non-generic ValueTask. Should be ValueTask<InvestigationFinding>.
    public async ValueTask HandleAsync(ClaimInput claim, IWorkflowContext context, CancellationToken ct)
    {
        await Task.Yield();
        // We "compute" a finding but discard it — the return-type-as-message
        // contract means nothing reaches the fan-in barrier.
        var finding = new InvestigationFinding(
            Investigator: "OSINT",
            RiskScore: 0.18,
            Notes: $"No adverse media for customer {claim.CustomerId}.");
        _ = finding; // explicitly throw it away to underline the bug
    }

    // MAF 1.2.0 required executors to override ConfigureProtocol to register
    // their message-handler dispatch. The 1.3 source generator removed this
    // requirement. Stubbed here so the file compiles; the protocol isn't
    // exercised at runtime by the sample (which is source-scanned, not run).
    protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder builder) => builder;
}
