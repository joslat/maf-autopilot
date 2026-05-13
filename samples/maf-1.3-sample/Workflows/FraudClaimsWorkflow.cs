// SPDX-License-Identifier: MIT
//
// ⚠️ DELIBERATE ANTI-PATTERN: fan-in barrier wired with the OBSOLETE overload.
// MAF 1.3.0 swapped the arg order:
//   1.2.0 era: AddFanInBarrierEdge(ExecutorBinding target, IEnumerable<ExecutorBinding> sources)  [Obsolete in 1.3.0]
//   1.3.0+   : AddFanInBarrierEdge(IEnumerable<ExecutorBinding> sources, ExecutorBinding target)
// Both overloads exist on the 1.3.0 surface; the first one is marked
// `[Obsolete("Use AddFanInBarrierEdge(IEnumerable<ExecutorBinding>, ExecutorBinding) instead.")]`.
// Calling the obsolete one yields CS0618 — visible by `MafRunCs0618Hunt`.
//
// Triggers:
//   - MAF130-FAN-IN-001 (registry; CS0618)

using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.Workflows;
using MafSample.FraudClaims.Agents;
using MafSample.FraudClaims.Executors;

namespace MafSample.FraudClaims.Workflows;

public static class FraudClaimsWorkflow
{
    public static Workflow Create(IChatClient chatClient)
    {
        var intake = IntakeAgent.Create(chatClient).BindAsExecutor(emitEvents: true);
        var decision = DecisionAgent.Create(chatClient).BindAsExecutor(emitEvents: true);

        // Raw Executor instances rely on the implicit `Executor → ExecutorBinding`
        // conversion when passed to AddEdge / AddFanInBarrierEdge.
        var osint = new OsintInvestigator();
        var history = new HistoryInvestigator();
        var transactions = new TransactionInvestigator();
        var aggregator = new AggregatorExecutor();
        var notify = new NotificationExecutor();

        var builder = new WorkflowBuilder(intake);

        // Fan-out from intake to the three investigators (correct).
        builder
            .AddEdge(intake, osint)
            .AddEdge(intake, history)
            .AddEdge(intake, transactions);

        // ⚠️ MAF130-FAN-IN-001 — OBSOLETE overload (target first, sources second).
        // The compiler emits CS0618 here. We intentionally let the warning flow
        // through (no #pragma suppression) so the maf-autopilot CS0618 hunter
        // (`MafRunCs0618Hunt`) can find this exact call site.
        // The corrected pattern would be:
        //   builder.AddFanInBarrierEdge(new ExecutorBinding[] { osint, history, transactions }, aggregator);
        builder.AddFanInBarrierEdge(
            aggregator,
            new ExecutorBinding[] { osint, history, transactions });

        builder
            .AddEdge(aggregator, decision)
            .AddEdge(decision, notify)
            .WithOutputFrom(notify);

        return builder.Build();
    }
}
