// SPDX-License-Identifier: MIT
// Snippet 3 — workflow wiring. Spot the bug. (~20 lines)

using Microsoft.Agents.AI.Workflows;

namespace FraudClaims.Workflows;

public static class FraudClaimsWorkflow
{
    public static Workflow Create()
    {
        var intake = new IntakeExecutor();
        var osint = new OsintInvestigator();
        var history = new HistoryInvestigator();
        var transactions = new TransactionInvestigator();
        var aggregator = new AggregatorExecutor();

        return new WorkflowBuilder(intake)
            .AddEdge(intake, osint)
            .AddEdge(intake, history)
            .AddEdge(intake, transactions)
            .AddFanInBarrierEdge(aggregator, new ExecutorBinding[] { osint, history, transactions })
            .Build();
    }
}
