using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SkSample;

// 🔁 REWRITE — the classic Semantic Kernel agent shape: build a Kernel, create a
// ChatCompletionAgent on it, open a thread, and invoke. Every piece here maps
// mechanically to MAF (drop the Kernel; AsAIAgent; CreateSessionAsync; RunAsync).
public sealed class ClaimAgent
{
    private readonly Kernel _kernel;
    private readonly ChatCompletionAgent _agent;

    public ClaimAgent(string modelId, string apiKey)
    {
        // Kernel + chat completion service → MAF removes the Kernel; create the
        // agent straight from an IChatClient.
        _kernel = Kernel.CreateBuilder()
            .AddOpenAIChatCompletion(modelId, apiKey)
            .Build();
        _kernel.Plugins.AddFromType<WeatherTools>();

        _agent = new ChatCompletionAgent
        {
            Instructions = "You triage insurance claims.",
            Kernel = _kernel,
        };
    }

    public async Task<string> TriageAsync(string claim)
    {
        // AgentThread → MAF AgentSession via agent.CreateSessionAsync().
        AgentThread thread = new ChatHistoryAgentThread();

        // *PromptExecutionSettings → ChatOptions / ChatClientAgentRunOptions.
        var settings = new OpenAIPromptExecutionSettings { MaxTokens = 1000 };
        var options = new AgentInvokeOptions { KernelArguments = new(settings) };

        // InvokeAsync → MAF RunAsync; AgentResponseItem → AgentResponse.
        await foreach (var item in _agent.InvokeAsync(claim, thread, options))
            return item.Message.Content ?? string.Empty;

        return string.Empty;
    }
}
