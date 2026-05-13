// SPDX-License-Identifier: MIT
//
// See Agents/IntakeAgent.cs for the explanation of the top-level-Instructions
// anti-pattern unreachability on 1.3.0 GA. This file uses the correct nested
// pattern.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MafSample.FraudClaims.Agents;

public static class DecisionAgent
{
    public static ChatClientAgent Create(IChatClient chatClient) =>
        new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "Decision",
            Description = "Decides accept / refer / reject based on aggregated investigator findings.",
            ChatOptions = new ChatOptions
            {
                Instructions = """
                    You receive aggregated evidence from three investigators (OSINT, history,
                    transactions). Each gives a risk score in [0.0, 1.0]. Decide:
                      - ACCEPT   if mean risk < 0.3
                      - REFER    if 0.3 <= mean risk < 0.7
                      - REJECT   if mean risk >= 0.7
                    Emit a TriageDecision JSON with Action, Confidence, and one-sentence Reasoning.
                    """,
            },
        });
}
