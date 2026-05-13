// SPDX-License-Identifier: MIT
//
// ⚠️ Note on the "top-level Instructions" anti-pattern:
//
// The maf-autopilot registry entry MAF130-INSTRUCTIONS-001 documents a
// "silently ignored Instructions property at the top of ChatClientAgentOptions."
// **This property does not exist on ChatClientAgentOptions in MAF 1.3.0 GA.**
// It was REMOVED outright, not just made [Obsolete]. So the anti-pattern as
// described in the registry cannot be reproduced — code referencing it
// won't compile.
//
// This is real Phase T feedback: the registry entry should be corrected to
// describe a pre-1.3.0 customer codebase pattern, not a 1.3.0 surface pattern.
// See README.md → "Phase T registry corrections".
//
// To preserve the spirit of the anti-pattern in this sample, we use the
// CORRECT 1.3 pattern (Instructions nested inside ChatOptions). The other
// anti-patterns in this sample still trigger the toolkit's scanners.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MafSample.FraudClaims.Agents;

public static class IntakeAgent
{
    public static ChatClientAgent Create(IChatClient chatClient) =>
        new ChatClientAgent(chatClient, new ChatClientAgentOptions
        {
            Name = "Intake",
            Description = "Parses a free-form claim into a structured ClaimInput.",
            ChatOptions = new ChatOptions
            {
                Instructions = """
                    You are the intake desk for a fraud-claims pipeline.
                    Read the raw claim text and emit a structured ClaimInput JSON.
                    Be terse. Do not invent fields that are not present in the text.
                    """,
            },
        });
}
