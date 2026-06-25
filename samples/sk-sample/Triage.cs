using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Planning;

namespace SkSample;

// 🏗 RE-ARCHITECT — multi-agent orchestration + a planner. AgentGroupChat maps to
// a MAF group-chat workflow; planners were removed entirely (use MAF's automatic
// function-calling loop). Neither has a 1:1 swap — these need a redesign.
public sealed class Triage
{
    public AgentGroupChat BuildReviewChat(ChatCompletionAgent writer, ChatCompletionAgent reviewer)
        => new(writer, reviewer)
        {
            ExecutionSettings = new AgentGroupChatSettings(),
        };

    public FunctionCallingStepwisePlanner BuildPlanner()
        => new(new FunctionCallingStepwisePlannerOptions { MaxIterations = 5 });
}
