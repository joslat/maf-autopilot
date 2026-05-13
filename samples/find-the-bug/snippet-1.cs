// SPDX-License-Identifier: MIT
// Snippet 1 — agent setup. Spot the bug. (~20 lines)

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;

namespace FraudClaims.Agents;

public static class IntakeAgent
{
    public static ChatClientAgent Create(IChatClient chatClient) =>
        new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "Intake",
            Description = "Parses a free-form claim into a structured ClaimInput.",
            Instructions = """
                You are the intake desk for a fraud-claims pipeline.
                Read the raw claim text and emit a structured ClaimInput JSON.
                """,
        });
}
