// SPDX-License-Identifier: MIT
//
// ⚠️ THIS FILE CONTAINS DELIBERATE ANTI-PATTERNS — DO NOT COPY INTO PRODUCTION.
// It exists to give the maf-autopilot migration toolkit something to find.
//
// Triggers:
//   - MAF-AP-SEC-001 / MAF002 — DefaultAzureCredential in production code
//   - MAF-AP-SEC-002          — Hard-coded API-key literal
//   - MAF-AP-SEC-003 / MAF003 — EnableSensitiveData = true outside dev
//   - MAF-AP-OBS-001          — Agent built without UseOpenTelemetry
//   - MAF130-MIDDLEWARE-001   — .Use() with only runFunc, runStreamingFunc=null → silent streaming bypass

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

namespace MafSample.FraudClaims;

public static class ChatClientFactory
{
    // MAF-AP-SEC-002 — hard-coded "api-key=" literal (placeholder, NOT real).
    // The maf-autopilot scanner flags this on regex; the real fix is to load
    // from configuration or Key Vault, never inline.
    private const string FALLBACK_KEY = "api-key=sk-FAKEPLACEHOLDERkey1234567890abcdef";

    public static IChatClient CreateChatClient()
    {
        var endpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? "https://example.openai.azure.com");

        // ⚠️ MAF-AP-SEC-001 / MAF002 — DefaultAzureCredential is too permissive in production.
        // It silently falls through to interactive auth, environment vars, managed identity,
        // CLI credential, etc. In a server-side workflow you want a single explicit
        // ManagedIdentityCredential or ClientSecretCredential.
        var credential = new DefaultAzureCredential();

        var azureClient = new AzureOpenAIClient(endpoint, credential);
        var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT") ?? "gpt-4o";

        // ⚠️ MAF-AP-OBS-001 — chained without UseOpenTelemetry().
        // A real production builder would add .UseOpenTelemetry() between
        // AsIChatClient() and AsBuilder() so spans land in the configured exporter.
        var rawClient = azureClient.GetChatClient(deployment).AsIChatClient();

        // ⚠️ MAF130-MIDDLEWARE-001 — only the run-func is wired up. The streaming
        // path bypasses this middleware entirely because the streaming Func is
        // null. Any RunStreamingAsync invocation flows around the cost-tracking
        // logic.
        //
        // (Real-API note: the registry documents this with named arguments
        // `runFunc:` / `runStreamingFunc:` but the actual ChatClientBuilder.Use
        // overload does not use those parameter names. The anti-pattern itself
        // is real — pass null for the streaming branch — but only positional
        // usage compiles. See README.md → "Phase T registry corrections".)
        return rawClient.AsBuilder()
            .Use(
                async (messages, options, next, ct) =>
                {
                    // pretend we record cost / tokens here for non-streaming calls
                    return await next.GetResponseAsync(messages, options, ct).ConfigureAwait(false);
                },
                null /* runStreamingFunc — null = streaming bypass */)
            .Build();
    }

    /// <summary>
    /// ⚠️ MAF-AP-SEC-003 / MAF003 — EnableSensitiveData = true is fine in dev
    /// but ships PII to the logger in prod. The analyzer flags this.
    /// </summary>
    public static ChatOptions BuildVerboseChatOptions()
        => new ChatOptions
        {
            // EnableSensitiveData = true ← the analyzer matches this exact text.
            // We keep it as the named property the scanner expects.
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["EnableSensitiveData"] = true,
            },
        };

    // Placeholder usage so the FALLBACK_KEY const is not dead-code-eliminated
    // by the compiler (we WANT the scanner to find it in the source text).
    public static string DebugKeyPrefix() => FALLBACK_KEY.Substring(0, 7);
}
